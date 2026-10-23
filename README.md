<!-- windesk-release:start -->
# WinDesk

Windows 10 ve 11 için masaüstü grupları, akordiyon panelleri ve sürükle-bırak kısayolları. C# / WPF ile geliştirilmiştir.

## İndir

**Güncel sürüm: 0.7.13** · [Sürüm notları](https://github.com/LogicalTR/WinDesk/releases/tag/v0.7.13)

- [Türkçe kurulum](https://github.com/LogicalTR/WinDesk/releases/download/v0.7.13/WinDesk-Setup-0.7.13-x64.exe)
- [Taşınabilir EXE](https://github.com/LogicalTR/WinDesk/releases/download/v0.7.13/WinDesk-0.7.13-win-x64.exe)
- [Taşınabilir ZIP ve kullanım kılavuzu](https://github.com/LogicalTR/WinDesk/releases/download/v0.7.13/WinDesk-0.7.13-win-x64.zip)
- [SHA-256 doğrulama listesi](https://github.com/LogicalTR/WinDesk/releases/download/v0.7.13/SHA256SUMS.txt)

Windows 10 22H2 (19045) ve üzeri, x64 gerekir. .NET çalışma zamanı dahildir. Bu sürümün EXE ve Setup dosyaları Authenticode ile imzalı değildir. SHA-256 listesi dosya bütünlüğünü doğrular; yayıncı imzasının yerine geçmez.

Kurulumun son ekranında **ByBrowser'ı da indirmek ister misiniz? (İndir ve kurulumu başlat)** seçeneği vardır. Varsayılan olarak kapalıdır; kendiniz seçip **Bitti**'ye bastığınızda [ByBrowser'ın en güncel kararlı sürümü](https://github.com/LogicalTR/ByBrowser-Releases/releases/latest) doğrudan indirilir. Dosya doğrulanınca ByBrowser kurulum sihirbazı otomatik açılır. İndirmek istemiyorsanız seçeneği kapalı bırakın. İlerleme penceresinde iptal, yeniden deneme ve **Dosyayı göster** bulunur. Sessiz WinDesk kurulumunda ByBrowser indirilmez.

## Kullanım ve güncelleme

**Ctrl + Shift** ile tüm panelleri öne getirin; tekrar kullanınca masaüstüne geri gönderin. Grup başlığına tıklamak seçilen grubu hemen açar; başlıkları sürükleyerek sıralama ve ayırma kullanılmaya devam eder.

Saat yanındaki WinDesk simgesinden yeni grup ekleyin veya ayarlara ulaşın. Kısayolları gruplara bırakın; başlıkları sürükleyerek birleştirin, sıralayın veya ayırın. Raptiye grubu açık tutar.

**Otomatik kapanma** süresi, panelden ayrılmanın yanında birleşik gruplar arasında başlıkla geçişte de uygulanır. Süre dolunca önceki grup kapanıp yeni grup açılır; önceki gruba geri dönmek geçişi iptal eder. Sabitlenmiş gruplar açık kalır.

Paneller ve birleşik alanlar yeniden açıldığında kayıtlı ekran konumlarına döner. İkinci ekran geç algılansa bile tercih edilen yer korunur.

Uygulama güncellemesi kısayolun özel simge dosyasını silerse uygulamanın kendi simgesi gösterilir; kısayolu yeniden eklemek gerekmez. Geçerli özel simgeler ve açılış ayarları korunur.

**Ayarlar → Güncellemeleri denetle** ile yeni sürümü bulun ve indirin. İndirme doğrulandıktan sonra onayınızla WinDesk kapanır ve kurulum açılır. Taşınabilir uygulamada bu seçenek kullanıcı hesabına kurulum yapar. Güncelleme ve kaldırma `%LOCALAPPDATA%\WinDesk` içindeki düzeni ve saklanan kısayolları silmez.

0.7.6 ve daha eski sürümlerde güncelleme düğmesi yoktur; yukarıdaki kurucuyu bir kez indirip çalışan WinDesk'i kapatarak kurun.

Masaüstü menüsü Windows 10'da **Sağ tık → WinDesk**, Windows 11'de **Sağ tık → Daha fazla seçenek göster → WinDesk** altındadır.

## Kaynak kodu

WinDesk MIT lisanslıdır. Kaynak kodu, testler ve derleme akışı [v0.7.13 etiketinde](https://github.com/LogicalTR/WinDesk/tree/v0.7.13) incelenebilir; [sürümle eşleşen kaynak ZIP'i](https://github.com/LogicalTR/WinDesk/releases/download/v0.7.13/WinDesk-0.7.13-source.zip) de yayın dosyalarındadır. Eski, yalnız dağıtım README'si içeren etiketlerin otomatik kaynak arşivi yerine ilgili sürümün ekli kaynak ZIP'ini kullanın.

## Code signing policy

[İmzalama politikası ve ekip rolleri](https://github.com/LogicalTR/WinDesk/blob/v0.7.13/CODE_SIGNING.md) · [Gizlilik](https://github.com/LogicalTR/WinDesk/blob/v0.7.13/packaging/PRIVACY.txt).
SignPath Foundation entegrasyonu hazırlanmıştır; kabul ve ilk imzalı yayın henüz tamamlanmamıştır. Geçmiş imzasız dosyalar geriye dönük imzalanmaz.
<!-- windesk-release:end -->
