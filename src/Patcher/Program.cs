using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Mono.Cecil;
using Mono.Cecil.Cil;
using Mono.Cecil.Rocks;

internal static class Program
{
    private static void Main(string[] args)
    {
        string gameRoot = args.Length > 0 && !string.IsNullOrWhiteSpace(args[0])
            ? args[0]
            : (Environment.GetEnvironmentVariable("HITC_GAME_DIR")
               ?? @"C:\Program Files (x86)\Steam\steamapps\common\Hope in the City");
        string gameManaged = Path.Combine(gameRoot, "HITC_Data", "Managed");
        string bakPath = Path.Combine(gameManaged, "Assembly-CSharp.dll.bak");
        string outPath = Path.Combine(gameManaged, "Assembly-CSharp.dll");
        string runtimeDll = args.Length > 1 && !string.IsNullOrWhiteSpace(args[1])
            ? args[1]
            : Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "RuntimeLib", "bin", "Release", "netstandard2.1", "RuntimeLib.dll"));

        if (!File.Exists(bakPath))
            throw new FileNotFoundException("Backup missing: " + bakPath);

        var resolver = new DefaultAssemblyResolver();
        resolver.AddSearchDirectory(gameManaged);

        using var targetAsm = AssemblyDefinition.ReadAssembly(bakPath, new ReaderParameters
        {
            AssemblyResolver = resolver,
            ReadSymbols = false
        });
        using var runtimeAsm = AssemblyDefinition.ReadAssembly(runtimeDll, new ReaderParameters
        {
            AssemblyResolver = resolver,
            ReadSymbols = false
        });

        var dstMod = targetAsm.MainModule;
        var srcMod = runtimeAsm.MainModule;

        var srcTypes = srcMod.Types.Where(t => t.Name != "<Module>").ToList();
        if (srcTypes.Count != 1 || srcTypes[0].Name != "TurkishPatchRuntime")
            throw new Exception("Unexpected types in RuntimeLib.dll: " + string.Join(", ", srcTypes.Select(t => t.FullName)));
        var srcType = srcTypes[0];
        if (srcType.HasNestedTypes)
            throw new Exception("TurkishPatchRuntime must not have compiler-generated nested types: " + string.Join(", ", srcType.NestedTypes.Select(t => t.Name)));

        // Remove any prior injection if somehow present
        var existingInjected = dstMod.Types.FirstOrDefault(t => t.FullName == "TurkishPatchRuntime");
        if (existingInjected != null)
            dstMod.Types.Remove(existingInjected);

        TypeDefinition injectedType = CloneType(srcMod, dstMod, srcType);
        dstMod.Types.Add(injectedType);

        // Locate injected methods
        MethodDefinition mEnsureInit = FindMethod(injectedType, "EnsureInitialized", 0);
        MethodDefinition mEnsureFont = FindMethod(injectedType, "EnsureFontFallbacks", 1);
        MethodDefinition mTranslate = FindMethod(injectedType, "Translate", 1);
        MethodDefinition mInitLang = FindMethod(injectedType, "GetInitialLanguageDisplayName", 1);
        MethodDefinition mGetSrt = FindMethod(injectedType, "GetSrtText", 1);

        // Apply exact hooks
        PatchLocalisationService(dstMod, mEnsureInit, mEnsureFont);
        PatchExtrasSubtitlePlayback(dstMod, mGetSrt, mEnsureFont);
        PatchGallery(dstMod, mTranslate);
        PatchMainMenu(dstMod, mTranslate);
        PatchOptions(dstMod, mTranslate, mInitLang);
        PatchPlayerMovement(dstMod, mTranslate);

        // Verify no self-reference or wrong framework references leaked in
        var badRefs = dstMod.AssemblyReferences
            .Where(r => r.Name == "Assembly-CSharp" || r.Name == "RuntimeLib" || r.Name == "mscorlib" || r.Name == "System.Private.CoreLib")
            .Select(r => r.FullName)
            .ToList();
        if (badRefs.Count > 0)
            throw new Exception("Bad assembly references detected: " + string.Join(", ", badRefs));

        targetAsm.Write(outPath);
        Console.WriteLine("Successfully wrote patched Assembly-CSharp.dll (" + new FileInfo(outPath).Length + " bytes).");
    }

    private static TypeDefinition FindType(ModuleDefinition mod, string fullName)
    {
        var t = mod.GetType(fullName);
        if (t == null)
            throw new Exception("Type not found in target assembly: " + fullName);
        return t;
    }

    private static MethodDefinition FindMethod(TypeDefinition type, string name, int paramCount)
    {
        var m = type.Methods.FirstOrDefault(x => x.Name == name && x.Parameters.Count == paramCount);
        if (m == null)
            throw new Exception($"Method not found: {type.FullName}::{name}({paramCount})");
        return m;
    }

    private static void WrapLdstrWithTranslate(MethodDefinition method, MethodDefinition mTranslate, HashSet<string> targets)
    {
        var body = method.Body;
        body.SimplifyMacros();
        var il = body.GetILProcessor();
        var calls = new List<(Instruction ldstr, string val)>();
        foreach (var ins in body.Instructions)
        {
            if (ins.OpCode == OpCodes.Ldstr && ins.Operand is string s && targets.Contains(s))
                calls.Add((ins, s));
        }
        if (calls.Count == 0)
            throw new Exception("No target ldstr found in " + method.FullName);

        foreach (var (ldstr, val) in calls)
        {
            var next = ldstr.Next;
            foreach (var other in body.Instructions)
            {
                if (other.Operand == next || (other.Operand is Instruction[] arr && arr.Contains(next)))
                    throw new Exception($"Branch targets instruction after ldstr '{val}' in {method.FullName}");
            }
            il.InsertAfter(ldstr, il.Create(OpCodes.Call, mTranslate));
            Console.WriteLine($"  Hooked ldstr \"{val}\" in {method.FullName}");
        }
        body.OptimizeMacros();
    }

    private static void PatchLocalisationService(ModuleDefinition mod, MethodDefinition mEnsureInit, MethodDefinition mEnsureFont)
    {
        var locType = FindType(mod, "KaiBanks_Localisation_2026.LocalisationService");

        // 1. SetCurrentLanguage(LanguageID, bool) -> call EnsureInitialized() at start
        var setCurr = locType.Methods.FirstOrDefault(m =>
            m.Name == "SetCurrentLanguage" && m.Parameters.Count == 2 &&
            m.Parameters[0].ParameterType.Name == "LanguageID");
        if (setCurr == null) throw new Exception("SetCurrentLanguage(LanguageID, bool) not found");
        setCurr.Body.SimplifyMacros();
        var ilSet = setCurr.Body.GetILProcessor();
        ilSet.InsertBefore(setCurr.Body.Instructions[0], ilSet.Create(OpCodes.Call, mEnsureInit));
        setCurr.Body.OptimizeMacros();
        Console.WriteLine("Hooked LocalisationService::SetCurrentLanguage(LanguageID, bool)");

        // 2. Get(string) -> call EnsureInitialized(); if (string.IsNullOrEmpty(key)) return "";
        var getStr = FindMethod(locType, "Get", 1);
        var extrasCreate = FindMethod(FindType(mod, "ExtrasSubtitlePlayback"), "Create", 5);
        var isNullOrEmptyRef = extrasCreate.Body.Instructions
            .Select(i => i.Operand as MethodReference)
            .FirstOrDefault(mr => mr != null && mr.Name == "IsNullOrEmpty" && mr.DeclaringType.Name == "String")
            ?? throw new Exception("Could not locate existing [netstandard]System.String::IsNullOrEmpty call");

        getStr.Body.SimplifyMacros();
        var ilGet = getStr.Body.GetILProcessor();
        var origFirst = getStr.Body.Instructions[0];
        var callInit = ilGet.Create(OpCodes.Call, mEnsureInit);
        var ldKey = ilGet.Create(OpCodes.Ldarg_1);
        var callEmpty = ilGet.Create(OpCodes.Call, isNullOrEmptyRef);
        var brFalse = ilGet.Create(OpCodes.Brfalse, origFirst);
        var ldEmpty = ilGet.Create(OpCodes.Ldstr, string.Empty);
        var retEmpty = ilGet.Create(OpCodes.Ret);

        ilGet.InsertBefore(origFirst, callInit);
        ilGet.InsertAfter(callInit, ldKey);
        ilGet.InsertAfter(ldKey, callEmpty);
        ilGet.InsertAfter(callEmpty, brFalse);
        ilGet.InsertAfter(brFalse, ldEmpty);
        ilGet.InsertAfter(ldEmpty, retEmpty);
        getStr.Body.OptimizeMacros();
        Console.WriteLine("Hooked LocalisationService::Get(string)");

        // 3. GetFontForLanguage(LanguageID, TMP_FontAsset) -> EnsureInitialized(); EnsureFontFallbacks(baseFont);
        var getFont = FindMethod(locType, "GetFontForLanguage", 2);
        getFont.Body.SimplifyMacros();
        var ilFont = getFont.Body.GetILProcessor();
        var f0 = getFont.Body.Instructions[0];
        var fInit = ilFont.Create(OpCodes.Call, mEnsureInit);
        var fArg1 = ilFont.Create(OpCodes.Ldarg_1);
        var fCall = ilFont.Create(OpCodes.Call, mEnsureFont);
        ilFont.InsertBefore(f0, fInit);
        ilFont.InsertAfter(fInit, fArg1);
        ilFont.InsertAfter(fArg1, fCall);
        getFont.Body.OptimizeMacros();
        Console.WriteLine("Hooked LocalisationService::GetFontForLanguage");
    }

    private static void PatchExtrasSubtitlePlayback(ModuleDefinition mod, MethodDefinition mGetSrt, MethodDefinition mEnsureFont)
    {
        var t = FindType(mod, "ExtrasSubtitlePlayback");

        // 1. Create(VideoManager, string, ExtrasSubtitleTrack[], TMP_FontAsset, float?)
        var mCreate = FindMethod(t, "Create", 5);
        mCreate.Body.SimplifyMacros();
        var il = mCreate.Body.GetILProcessor();
        bool replaced = false;
        foreach (var ins in mCreate.Body.Instructions)
        {
            if (ins.OpCode == OpCodes.Callvirt && ins.Operand is MethodReference mr &&
                mr.Name == "get_text" && mr.DeclaringType.Name == "TextAsset")
            {
                ins.OpCode = OpCodes.Call;
                ins.Operand = mGetSrt;
                replaced = true;
                break;
            }
        }
        if (!replaced) throw new Exception("TextAsset::get_text not found in ExtrasSubtitlePlayback::Create");
        mCreate.Body.OptimizeMacros();
        Console.WriteLine("Hooked ExtrasSubtitlePlayback::Create (GetSrtText)");

        // 2. CreateSubtitleDisplay(TMP_FontAsset, ExtrasSubtitleTrack) -> call EnsureFontFallbacks(track.srtFontOverride)
        var mDisp = FindMethod(t, "CreateSubtitleDisplay", 2);
        mDisp.Body.SimplifyMacros();
        var ilDisp = mDisp.Body.GetILProcessor();
        Instruction targetSetFont = null;
        foreach (var ins in mDisp.Body.Instructions)
        {
            if (ins.OpCode == OpCodes.Callvirt && ins.Operand is MethodReference mr &&
                mr.Name == "set_font" && ins.Previous?.OpCode == OpCodes.Ldfld &&
                ins.Previous.Operand is FieldReference fr && fr.Name == "srtFontOverride")
            {
                targetSetFont = ins;
                break;
            }
        }
        if (targetSetFont == null) throw new Exception("srtFontOverride set_font not found in CreateSubtitleDisplay");
        var ldTrack = ilDisp.Create(OpCodes.Ldarg_2);
        var ldFont = ilDisp.Create(OpCodes.Ldfld, targetSetFont.Previous.Operand as FieldReference);
        var callEnsure = ilDisp.Create(OpCodes.Call, mEnsureFont);
        ilDisp.InsertAfter(targetSetFont, ldTrack);
        ilDisp.InsertAfter(ldTrack, ldFont);
        ilDisp.InsertAfter(ldFont, callEnsure);
        mDisp.Body.OptimizeMacros();
        Console.WriteLine("Hooked ExtrasSubtitlePlayback::CreateSubtitleDisplay (EnsureFontFallbacks)");
    }

    private static void PatchGallery(ModuleDefinition mod, MethodDefinition mTranslate)
    {
        var t = FindType(mod, "UIStackable_Gallery");
        WrapLdstrWithTranslate(FindMethod(t, "OpenGallery", 0), mTranslate, new HashSet<string> { "Gallery" });
        WrapLdstrWithTranslate(FindMethod(t, "OpenExtras", 0), mTranslate, new HashSet<string> { "Extras" });
    }

    private static void PatchMainMenu(ModuleDefinition mod, MethodDefinition mTranslate)
    {
        var t = FindType(mod, "UIStack_MainMenu");
        WrapLdstrWithTranslate(FindMethod(t, "BeginDeleteConfirmation", 0), mTranslate, new HashSet<string> { "Delete this save?" });
    }

    private static void PatchOptions(ModuleDefinition mod, MethodDefinition mTranslate, MethodDefinition mInitLang)
    {
        var t = FindType(mod, "UIStackable_Options");
        WrapLdstrWithTranslate(FindMethod(t, "SetDemoIdleSecondsLabel", 1), mTranslate, new HashSet<string> { "Demo Idle Seconds: " });

        var mStart = FindMethod(t, "Start", 0);
        mStart.Body.SimplifyMacros();
        var il = mStart.Body.GetILProcessor();
        bool hooked = false;
        foreach (var ins in mStart.Body.Instructions)
        {
            if (ins.OpCode == OpCodes.Stfld && ins.Operand is FieldReference fr && fr.Name == "initialLocName")
            {
                il.InsertBefore(ins, il.Create(OpCodes.Call, mInitLang));
                hooked = true;
                break;
            }
        }
        if (!hooked) throw new Exception("initialLocName stfld not found in UIStackable_Options::Start");
        mStart.Body.OptimizeMacros();
        Console.WriteLine("Hooked UIStackable_Options::Start (GetInitialLanguageDisplayName)");
    }

    private static void PatchPlayerMovement(ModuleDefinition mod, MethodDefinition mTranslate)
    {
        var t = FindType(mod, "PlayerMovement");
        var targets = new HashSet<string>
        {
            "R Dismiss<br>T Close<br>Y Sketchbook",
            "R Phone<br>T Close<br>Y Dismiss",
            "R Phone",
            "T Close",
            "Y Sketchbook"
        };
        WrapLdstrWithTranslate(FindMethod(t, "Update", 0), mTranslate, targets);
    }

    private static TypeDefinition CloneType(ModuleDefinition srcMod, ModuleDefinition dstMod, TypeDefinition srcType)
    {
        TypeDefinition dstType = null;
        dstType = new TypeDefinition(srcType.Namespace, srcType.Name, srcType.Attributes, ImportType(srcType.BaseType));

        var fieldMap = new Dictionary<string, FieldDefinition>();
        foreach (var f in srcType.Fields)
        {
            var nf = new FieldDefinition(f.Name, f.Attributes, ImportType(f.FieldType));
            dstType.Fields.Add(nf);
            fieldMap[f.Name] = nf;
        }

        var methodMap = new Dictionary<MethodDefinition, MethodDefinition>();
        foreach (var m in srcType.Methods)
        {
            var nm = new MethodDefinition(m.Name, m.Attributes, ImportType(m.ReturnType))
            {
                ImplAttributes = m.ImplAttributes,
                HasThis = m.HasThis,
                ExplicitThis = m.ExplicitThis,
                CallingConvention = m.CallingConvention
            };
            foreach (var p in m.Parameters)
            {
                var np = new ParameterDefinition(p.Name, p.Attributes, ImportType(p.ParameterType));
                nm.Parameters.Add(np);
            }
            dstType.Methods.Add(nm);
            methodMap[m] = nm;
        }

        foreach (var m in srcType.Methods)
        {
            if (!m.HasBody) continue;
            var nm = methodMap[m];
            var nb = nm.Body;
            nb.InitLocals = m.Body.InitLocals;
            nb.MaxStackSize = m.Body.MaxStackSize;

            var varMap = new Dictionary<VariableDefinition, VariableDefinition>();
            foreach (var v in m.Body.Variables)
            {
                var nv = new VariableDefinition(ImportType(v.VariableType));
                nb.Variables.Add(nv);
                varMap[v] = nv;
            }

            var insMap = new Dictionary<Instruction, Instruction>();
            var clonedList = new List<(Instruction orig, Instruction clone)>();
            foreach (var ins in m.Body.Instructions)
            {
                var c = CloneInstructionShell(ins, dstMod, srcType, dstType, fieldMap, methodMap, varMap, nm);
                insMap[ins] = c;
                nb.Instructions.Add(c);
                clonedList.Add((ins, c));
            }

            // Second pass: resolve Instruction / Instruction[] branch & switch targets
            foreach (var (orig, c) in clonedList)
            {
                if (orig.Operand is Instruction target)
                    c.Operand = insMap[target];
                else if (orig.Operand is Instruction[] targets)
                    c.Operand = targets.Select(t => insMap[t]).ToArray();
            }

            foreach (var eh in m.Body.ExceptionHandlers)
            {
                var neh = new ExceptionHandler(eh.HandlerType)
                {
                    TryStart = eh.TryStart != null ? insMap[eh.TryStart] : null,
                    TryEnd = eh.TryEnd != null ? insMap[eh.TryEnd] : null,
                    FilterStart = eh.FilterStart != null ? insMap[eh.FilterStart] : null,
                    HandlerStart = eh.HandlerStart != null ? insMap[eh.HandlerStart] : null,
                    HandlerEnd = eh.HandlerEnd != null ? insMap[eh.HandlerEnd] : null,
                    CatchType = eh.CatchType != null ? ImportType(eh.CatchType) : null
                };
                nb.ExceptionHandlers.Add(neh);
            }
        }

        return dstType;

        TypeReference ImportType(TypeReference tr)
        {
            if (tr == null) return null;
            if (tr.FullName == srcType.FullName) return dstType;
            if (tr is GenericInstanceType git)
            {
                var elem = ImportType(git.ElementType);
                var ngit = new GenericInstanceType(elem);
                foreach (var a in git.GenericArguments)
                    ngit.GenericArguments.Add(ImportType(a));
                return ngit;
            }
            if (tr is ArrayType at)
                return new ArrayType(ImportType(at.ElementType), at.Rank);
            if (tr is ByReferenceType brt)
                return new ByReferenceType(ImportType(brt.ElementType));
            if (tr is GenericParameter)
                return tr;

            if (tr.Scope != null && tr.Scope.Name == "Assembly-CSharp")
            {
                var resolved = ResolveInTarget(tr);
                if (resolved == null)
                    throw new Exception("Could not resolve Assembly-CSharp type in target: " + tr.FullName);
                return resolved;
            }
            return dstMod.ImportReference(tr);
        }

        TypeDefinition ResolveInTarget(TypeReference tr)
        {
            if (tr.DeclaringType != null)
            {
                var parent = ResolveInTarget(tr.DeclaringType);
                return parent?.NestedTypes.FirstOrDefault(nt => nt.Name == tr.Name);
            }
            return dstMod.GetType(tr.FullName);
        }

        MethodReference ImportMethod(MethodReference mr)
        {
            if (mr.DeclaringType.FullName == srcType.FullName)
            {
                var srcDef = mr.Resolve();
                if (srcDef != null && methodMap.TryGetValue(srcDef, out var mapped))
                    return mapped;
                throw new Exception("Missing mapped internal method: " + mr.FullName);
            }

            if (mr is GenericInstanceMethod gim)
            {
                var elem = ImportMethod(gim.ElementMethod);
                var ngim = new GenericInstanceMethod(elem);
                foreach (var ga in gim.GenericArguments)
                    ngim.GenericArguments.Add(ImportType(ga));
                return ngim;
            }

            if (mr.DeclaringType.Scope != null && mr.DeclaringType.Scope.Name == "Assembly-CSharp")
            {
                var td = ResolveInTarget(mr.DeclaringType);
                var md = td.Methods.FirstOrDefault(m =>
                    m.Name == mr.Name && m.Parameters.Count == mr.Parameters.Count &&
                    m.Parameters.Select((p, i) => p.ParameterType.FullName == mr.Parameters[i].ParameterType.FullName).All(b => b));
                if (md == null)
                    throw new Exception("Could not resolve Assembly-CSharp method: " + mr.FullName);
                return md;
            }

            if (mr.DeclaringType is GenericInstanceType git)
            {
                if (!git.GenericArguments.Any(a => a.Scope?.Name == "Assembly-CSharp"))
                    return dstMod.ImportReference(mr);

                var elemDef = mr.Resolve() ?? throw new Exception("Cannot resolve method: " + mr.FullName);
                var importedDecl = ImportType(git);
                var nmr = new MethodReference(mr.Name, ImportOpenRef(elemDef.ReturnType), importedDecl)
                {
                    HasThis = mr.HasThis,
                    ExplicitThis = mr.ExplicitThis,
                    CallingConvention = mr.CallingConvention
                };
                foreach (var gp in elemDef.GenericParameters)
                    nmr.GenericParameters.Add(new GenericParameter(gp.Name, nmr));
                foreach (var p in elemDef.Parameters)
                    nmr.Parameters.Add(new ParameterDefinition(p.Name, p.Attributes, ImportOpenRef(p.ParameterType)));
                return dstMod.ImportReference(nmr);
            }

            return dstMod.ImportReference(mr);
        }

        TypeReference ImportOpenRef(TypeReference tr)
        {
            if (tr is GenericParameter) return tr;
            if (tr is ByReferenceType brt && ContainsGenericParam(brt.ElementType))
                return new ByReferenceType(ImportOpenRef(brt.ElementType));
            if (tr is ArrayType at && ContainsGenericParam(at.ElementType))
                return new ArrayType(ImportOpenRef(at.ElementType), at.Rank);
            return dstMod.ImportReference(tr);
        }

        static bool ContainsGenericParam(TypeReference tr) =>
            tr is GenericParameter ||
            (tr is ByReferenceType brt && ContainsGenericParam(brt.ElementType)) ||
            (tr is ArrayType at && ContainsGenericParam(at.ElementType));

        FieldReference ImportField(FieldReference fr)
        {
            if (fr.DeclaringType.FullName == srcType.FullName)
                return fieldMap[fr.Name];
            if (fr.DeclaringType.Scope != null && fr.DeclaringType.Scope.Name == "Assembly-CSharp")
            {
                var td = ResolveInTarget(fr.DeclaringType);
                return td.Fields.First(f => f.Name == fr.Name);
            }
            return dstMod.ImportReference(fr);
        }

        Instruction CloneInstructionShell(
            Instruction ins,
            ModuleDefinition dm,
            TypeDefinition st,
            TypeDefinition dt,
            Dictionary<string, FieldDefinition> fmap,
            Dictionary<MethodDefinition, MethodDefinition> mmap,
            Dictionary<VariableDefinition, VariableDefinition> vmap,
            MethodDefinition newMethod)
        {
            object op = ins.Operand;
            if (op == null)
                return Instruction.Create(ins.OpCode);
            if (op is Instruction)
                return Instruction.Create(ins.OpCode, Instruction.Create(OpCodes.Nop));
            if (op is Instruction[])
                return Instruction.Create(ins.OpCode, Array.Empty<Instruction>());

            object newOp = op switch
            {
                sbyte sb => sb,
                byte b => b,
                int i => i,
                long l => l,
                float fl => fl,
                double db => db,
                string s => s,
                TypeReference tr => ImportType(tr),
                MethodReference mr => ImportMethod(mr),
                FieldReference fr => ImportField(fr),
                VariableDefinition vd => vmap[vd],
                ParameterDefinition pd => pd.Index == -1 ? newMethod.Body.ThisParameter : newMethod.Parameters[pd.Index],
                _ => throw new NotSupportedException("Unsupported IL operand type: " + op.GetType().FullName)
            };

            return newOp switch
            {
                sbyte sb => Instruction.Create(ins.OpCode, sb),
                byte b => Instruction.Create(ins.OpCode, b),
                int i => Instruction.Create(ins.OpCode, i),
                long l => Instruction.Create(ins.OpCode, l),
                float fl => Instruction.Create(ins.OpCode, fl),
                double db => Instruction.Create(ins.OpCode, db),
                string s => Instruction.Create(ins.OpCode, s),
                TypeReference tr => Instruction.Create(ins.OpCode, tr),
                MethodReference mr => Instruction.Create(ins.OpCode, mr),
                FieldReference fr => Instruction.Create(ins.OpCode, fr),
                VariableDefinition vd => Instruction.Create(ins.OpCode, vd),
                ParameterDefinition pd => Instruction.Create(ins.OpCode, pd),
                _ => throw new InvalidOperationException()
            };
        }
    }
}
