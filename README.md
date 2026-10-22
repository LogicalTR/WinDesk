<!-- windesk-release:start -->
# WinDesk

Windows 10 ve 11 için masaüstü grupları, akordiyon panelleri ve sürükle-bırak kısayolları. C# / WPF ile geliştirilmiştir.

## İndir

**Güncel sürüm: 0.7.11** · [Sürüm notları](https://github.com/LogicalTR/WinDesk/releases/tag/v0.7.11)

- [Türkçe kurulum](https://github.com/LogicalTR/WinDesk/releases/download/v0.7.11/WinDesk-Setup-0.7.11-x64.exe)
- [Taşınabilir EXE](https://github.com/LogicalTR/WinDesk/releases/download/v0.7.11/WinDesk-0.7.11-win-x64.exe)
- [Taşınabilir ZIP ve kullanım kılavuzu](https://github.com/LogicalTR/WinDesk/releases/download/v0.7.11/WinDesk-0.7.11-win-x64.zip)
- [SHA-256 doğrulama listesi](https://github.com/LogicalTR/WinDesk/releases/download/v0.7.11/SHA256SUMS.txt)

Windows 10 22H2 (19045) ve üzeri, x64 gerekir. .NET çalışma zamanı dahildir. EXE dosyaları henüz Authenticode ile imzalı değildir.

Kurulumun son ekranında **ByBrowser'ı da indirmek ister misiniz? (İndir ve kurulumu başlat)** seçeneği vardır. Varsayılan olarak seçilidir; seçili bırakıp **Bitti**'ye bastığınızda [ByBrowser'ın en güncel kararlı sürümü](https://github.com/LogicalTR/ByBrowser-Releases/releases/latest) doğrudan indirilir. Dosya doğrulanınca ByBrowser kurulum sihirbazı otomatik açılır. İndirmek istemiyorsanız işareti kaldırın. İlerleme penceresinde iptal, yeniden deneme ve **Dosyayı göster** bulunur. Sessiz WinDesk kurulumunda ByBrowser indirilmez.

## Kullanım ve güncelleme

Saat yanındaki WinDesk simgesinden yeni grup ekleyin veya ayarlara ulaşın. Kısayolları gruplara bırakın; başlıkları sürükleyerek birleştirin, sıralayın veya ayırın. Raptiye grubu açık tutar.

**Otomatik kapanma** süresi, panelden ayrılmanın yanında birleşik gruplar arasında başlıkla geçişte de uygulanır. Süre dolunca önceki grup kapanıp yeni grup açılır; önceki gruba geri dönmek geçişi iptal eder. Sabitlenmiş gruplar açık kalır.

Paneller ve birleşik alanlar yeniden açıldığında kayıtlı ekran konumlarına döner. İkinci ekran geç algılansa bile tercih edilen yer korunur.

Uygulama güncellemesi kısayolun özel simge dosyasını silerse uygulamanın kendi simgesi gösterilir; kısayolu yeniden eklemek gerekmez. Geçerli özel simgeler ve açılış ayarları korunur.

**Ayarlar → Güncellemeleri denetle** ile yeni sürümü bulun ve indirin. İndirme doğrulandıktan sonra onayınızla WinDesk kapanır ve kurulum açılır. Taşınabilir uygulamada bu seçenek kullanıcı hesabına kurulum yapar. Güncelleme ve kaldırma `%LOCALAPPDATA%\WinDesk` içindeki düzeni ve saklanan kısayolları silmez.

0.7.6 ve daha eski sürümlerde güncelleme düğmesi yoktur; yukarıdaki kurucuyu bir kez indirip çalışan WinDesk'i kapatarak kurun.

Masaüstü menüsü Windows 10'da **Sağ tık → WinDesk**, Windows 11'de **Sağ tık → Daha fazla seçenek göster → WinDesk** altındadır.

## Kaynak kodu

Bu depo yayınları dağıtır. Tam C# / WPF projesi, testler ve derleme betikleri [WinDesk-0.7.11-source.zip](https://github.com/LogicalTR/WinDesk/releases/download/v0.7.11/WinDesk-0.7.11-source.zip) dosyasındadır. GitHub'ın otomatik **Source code** arşivleri yalnız dağıtım deposunu içerir.
<!-- windesk-release:end -->
