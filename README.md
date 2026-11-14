<div align="center">

# Hope in the City — Türkçe Yama 🇹🇷

[![Sürüm](https://img.shields.io/badge/Oyun%20S%C3%BCr%C3%BCm%C3%BC-V%201.2.0.0-2ea44f?style=flat-square)](https://github.com/kircastudios/Hope_in_the_City_TURKISH/releases/latest)
[![Kapsam](https://img.shields.io/badge/%C3%87eviri-3.065%20Sat%C4%B1r%20%2B%206%20Video-0969da?style=flat-square)](#mod-ve-%C3%A7eviri-%C3%B6zellikleri)
[![Yapım](https://img.shields.io/badge/Gemini%204-Hydrogen%20Extra-8957e5?style=flat-square)](#hakk%C4%B1nda)

**Orijinal font dokusunu bozmadan hazırlanmış, minimal ve tam kapsamlı Türkçe yerelleştirme yaması.**

<br>

<a href="https://github.com/kircastudios/Hope_in_the_City_TURKISH/releases/latest/download/Hope_in_the_City_Turkce_Yama_v1.2.0.0.zip"><img src="https://img.shields.io/badge/%E2%AC%87%20%C4%B0ND%C4%B0R-238636?style=for-the-badge" alt="İndir"></a>&nbsp;&nbsp;<a href="https://github.com/kircastudios/Hope_in_the_City_TURKISH/releases"><img src="https://img.shields.io/badge/T%C3%9CM%20S%C3%9CR%C3%9CMLER-1f6feb?style=for-the-badge&logo=github&logoColor=white" alt="Tüm Sürümler"></a>&nbsp;&nbsp;<a href="https://store.steampowered.com/app/3610790/Hope_in_the_City/"><img src="https://img.shields.io/badge/STEAM%20SAYFASI-171a21?style=for-the-badge&logo=steam&logoColor=white" alt="Steam Sayfası"></a>

</div>

---

## Hakkında

**[Hope in the City](https://store.steampowered.com/app/3610790/Hope_in_the_City/)** oyunu için hazırlanan bu yama, oyunun kendi görsel kimliğini ve el yazısı fontlarını koruyarak oyunu baştan sona Türkçeye çevirir.

> [!NOTE]
> Bu projenin Türkçe çevirisi ve çalışma zamanı patcher modu **Gemini 4 Hydrogen Extra** ile yapılmıştır.

> [!IMPORTANT]
> **Uyumlu Sürüm:** Mevcut Steam sürümü olan **V 1.2.0.0** üzerinde sorunsuz çalışır.

> [!WARNING]
> **Güncelleme Uyarısı:** Oyuna ileride gelebilecek Steam güncellemeleri `Assembly-CSharp.dll` ve dil dosyalarının üzerine yazabileceği için yamayı **bozabilir veya devre dışı bırakabilir**. Böyle bir durumda yama klasöründeki **`Yama_Kaldir.bat`** dosyasını çalıştırarak orijinal dosyalara dönebilir veya yamanın güncel sürümünü bekleyebilirsiniz.

---

## Mod ve Çeviri Özellikleri

| Özellik | Açıklama |
| :--- | :--- |
| **Tam Kapsamlı Çeviri** | `3.065` oyun içi metin satırı (diyaloglar, not defteri, düşünceler, çıkarımlar, atılımlar, görevler, arayüz, harita ve kayıt etiketleri) eksiksiz Türkçedir. |
| **Belgesel Altyazıları** | *Ekstralar* bölümündeki **6 videonun** tamamı (`.srt`) V 1.2.0.0 zamanlamalarıyla senkronize şekilde Türkçeye çevrilmiştir. |
| **Orijinal Font Koruması** | Stilize el yazısı ve arayüz fontları (`Kanit`, `Tektur`, `FuzzyBubbles`, el yazısı notlar) değiştirilmez; eksik Türkçe karakterler (`Ç, Ğ, İ, Ö, Ş, Ü, ç, ğ, ı, ö, ş, ü`) için çalışma zamanında dinamik glif tablosu ve `GoogleSans` yedeği üretilir. |
| **El Konsolu Modu Desteği** | V 1.2.0.0 güncellemesiyle eklenen *El Konsolu Modu (Handheld Mode)* açılıp kapatıldığında Türkçe karakter desteği sorunsuz devam eder. |
| **Minimal ve Temiz Yapı** | Unity `.assets` dosyalarına dokunulmaz; BepInEx gibi ek mod yükleyici gerekmez. İngilizce (`en-US`) yuvası üzerinden çalışır ve oyun ilk açılışta doğrudan **Türkçe** başlar. |
| **Hazır Yedek & Kolay Kaldırma** | Arşiv içinde orijinal V 1.2.0.0 `.bak` dosyaları hazır olarak gelir; tek tıkla **`Yama_Kaldir.bat`** çalıştırılarak oyun anında orijinal hâline döndürülür. |

---

## Kurulum ve Kaldırma

### Kurulum
1. Yukarıdaki **[İndir](https://github.com/kircastudios/Hope_in_the_City_TURKISH/releases/latest/download/Hope_in_the_City_Turkce_Yama_v1.2.0.0.zip)** butonuna tıklayarak `Hope_in_the_City_Turkce_Yama_v1.2.0.0.zip` arşivini indirin.
2. Arşivdeki **`HITC_Data`** klasörünü ve **`Yama_Kaldir.bat`** dosyasını oyunun kurulu olduğu ana klasöre (`HITC.exe` dosyasının yanına) atın ve **hedef dosyaları değiştirin**.
   > Varsayılan Steam yolu: `C:\Program Files (x86)\Steam\steamapps\common\Hope in the City`
3. Oyunu Steam'den başlatın — doğrudan **Türkçe** açılacaktır.

### Kaldırma (Orijinale Dönüş)
- Oyun klasörüne attığınız **`Yama_Kaldir.bat`** dosyasını çalıştırın (gerekirse sağ tıklayıp **Yönetici olarak çalıştır** seçin).
- Arşivle birlikte gelen orijinal **`Assembly-CSharp.dll.bak`** ve **`en-US.csv.bak`** yedekleri otomatik olarak geri yüklenir ve eklenen `.srt` altyazıları temizlenir.

---

## Oyun İçi Ekran Görüntüleri

| Ana Menü (`Main_Menu.png`) | Ayarlar (`Settings.png`) |
| :---: | :---: |
| ![Ana Menü](screenshots/Main_Menu.png) | ![Ayarlar](screenshots/Settings.png) |

| Not Defteri (`Journal.png`) | Etkileşim Seçenekleri (`InGame_3.png`) |
| :---: | :---: |
| ![Not Defteri](screenshots/Journal.png) | ![Etkileşim Seçenekleri](screenshots/InGame_3.png) |

| Arayüz — Mod Bilgilendirme (`UI_1.png`) | Arayüz — Kayıt Ekranı (`UI_2.png`) |
| :---: | :---: |
| ![Mod Bilgilendirme](screenshots/UI_1.png) | ![Kayıt Ekranı](screenshots/UI_2.png) |

| Oyun İçi Diyalog 1 (`InGame_1.png`) | Oyun İçi Diyalog 2 (`InGame_2.png`) |
| :---: | :---: |
| ![Oyun İçi Diyalog 1](screenshots/InGame_1.png) | ![Oyun İçi Diyalog 2](screenshots/InGame_2.png) |

<div align="center">

**Oyun İçi Diyalog 3 (`InGame_4.png`)**

<img src="screenshots/InGame_4.png" alt="Oyun İçi Diyalog 3" width="75%">

</div>

---

## Geliştiriciler İçin Depo Yapısı

- `src/RuntimeLib/` — Çalışma zamanı font fallback ve harici metin/SRT yükleyici C# kodu (`TurkishPatchRuntime.cs`).
- `src/Patcher/` — `Mono.Cecil` tabanlı IL yama aracı (`Program.cs`).
- `screenshots/` — V 1.2.0.0 sürümünden alınan oyun içi ekran görüntüleri.
