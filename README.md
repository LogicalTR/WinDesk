# WinDesk

Windows 10 ve 11 için C# / WPF ile geliştirilen kişisel masaüstü düzenleyici. Gruplar Explorer'ın masaüstü katmanında yaşar; bağımsız uygulama penceresi olarak öne çıkmaz. Gruplar isteğe bağlı olarak tek bir akordiyon alanında birleştirilebilir.

## Çalıştırma

[GitHub Releases](https://github.com/LogicalTR/WinDesk/releases/latest) yayımlanmış sürümleri içerir. Bu kaynak sürümünün kurucusu **WinDesk-Setup-0.7.6-x64.exe** dosyasıdır. Türkçe kurucu yalnız mevcut kullanıcıya kurar; masaüstü/Başlat kısayollarını, klasik sağ tık menüsünü ve isteğe bağlı Windows başlangıcını hazırlar. Hedef sistem Windows 10 22H2 (19045) ve üzeri, x64'tür. Yayın paketi .NET çalışma zamanını içerir. Windows 10 22H2 üzerinde gerçek sanal makine doğrulaması tamamlandı; ortam ve kapsam aşağıdaki test matrisindedir.

Kurulum istemiyorsanız **WinDesk-0.7.6-win-x64.exe** dosyasını doğrudan açın veya ZIP paketini çıkarıp `WinDesk.exe` dosyasını çalıştırın. Paneller başlar; ayrı bir yönetim penceresi açılmaz. Genel tercihlere saat yanındaki **Ayarlar** komutuyla ulaşılır. Kurucunun sağ tık menüsü Windows 10'da **masaüstü sağ tık → WinDesk**, Windows 11'de **Daha fazla seçenek göster → WinDesk** altındadır; aşağıdaki modern menü MSIX adımları geliştiriciler içindir.

Yeni sürümü kurmadan önce saat yanındaki WinDesk menüsünden **Çıkış**'ı seçin. Güncelleme ve kaldırma `%LOCALAPPDATA%\WinDesk` içindeki düzeni ve saklanan kısayolları silmez. Otomatik güncelleme yoktur. EXE dosyaları henüz Authenticode yayıncı imzası taşımaz; indirme bütünlüğü için `SHA256SUMS.txt` sunulur.

- Küçük panel başlığına fareyle yaklaşınca uygulamalar açılır.
- Fare uzaklaşınca sabitlenmemiş panel kapanır. **Ayarlar → Otomatik kapanma** ile 0,20–2,00 saniye seçilir. Değişiklik bekleyen kapanma sayacına da hemen uygulanır.
- Her grup başlığındaki **raptiye** simgesi o grubu açık tutar. Etkin simge yeşildir; yeniden tıklayınca otomatik kapanmaya döner. Sabitleme tercihi yeniden başlatınca korunur. Birden fazla grup aynı anda sabitlenebilir; başka bir bölümün açılması onları kapatmaz. Sabitleme, diğer uygulamaların üzerine çıkarmak veya konumu kilitlemek anlamına gelmez.
- Başlıktan sürükleyerek paneli taşıyın.
- Bir grubun başlığını başka grubun veya birleşik alanın üzerine taşıyın. **Birleştirmek için bırak** işareti görünürken fareyi bırakınca gruplar hedefin konumunda birleşir. Birleşik alanın içindeki başlıkla sürükleme yalnızca o grubu taşır. İki birleşik alanı tüm üyeleriyle birleştirmek için soldaki noktalı tutamacı sürükleyin. Hedef grubun bölümleri önce, taşınan bölümler sonra yer alır; kısayollar korunur.
- Birleşik alanda grup başlıkları birlikte durur. Fareyle bir başlığın üzerine gelince o bölüm açılır, sabitlenmemiş önceki bölüm kapanır. Başlığın solundaki **noktalı tutamaç** bütün alanı birlikte taşır.
- Birleşik alan içindeki başlıkları **başlık metninden tutup yukarı veya aşağı sürükleyerek sıralayın**. Yeşil çizgi ve **Buraya taşı** etiketi yeni konumu gösterir; bırakınca sıra kaydedilir. Sabitlenen gruplar açık kalır, kısayollar ve alanın konumu korunur. Uzun alanlarda üst/alt kenarda bekleyerek kaydırabilirsiniz. Noktalı tutamaç tüm alanı birlikte taşır.
- Bir grubu birleşik alandan ayırmak için **kendi başlığını alanın dışına sürükleyip masaüstüne bırakın**. Alanın dışında taşınan grubun önizlemesi imleci izler; kalan gruplar yerlerinde durur. İçeri geri dönünce sıralama çizgisi görünür. `Esc` veya sürüklemenin kesilmesi işlemi iptal eder. Tek üye kalan birleşik alan kendiliğinden bağımsız panele dönüşür. Sabitleme ve kısayollar korunur.
- Grup menüsündeki **Birleşik alanı ayır** ile aynı grupları ve içeriklerini yeniden bağımsız hale getirin. Birleşik alana başka bir grubu sürükleyerek ekleyebilirsiniz.
- Masaüstündeki bir `.lnk`, `.url`, uygulama, dosya veya klasörü panele bırakın. Başlıktaki **+** veya grup menüsündeki **Kısayol ekle / Klasör ekle** seçenekleri de kullanılabilir.
- Bir simgeye tek tıklama öğeyi açar. Sağ tık → **Panelden kaldır**, yalnızca WinDesk içindeki kaydı kaldırır.
- Kısayolu **kendi grubunda sürükleyerek sıralayın**; yeşil çizgi bırakılacağı aralığı gösterir. Başka grubun gövdesine ya da başlığına bırakınca o gruba taşınır. Kapalı grup üzerine gelince açılır; uzun listelerde üst/alt kenarda bekleyerek kaydırabilirsiniz. Aynı kısayol hedefte varsa tek kayıt olarak tutulur.
- Kısayolu **panellerin dışındaki masaüstüne bırakınca** gruptan çıkar. Zaten kişisel veya ortak masaüstünde bulunan `.lnk` / `.url` tekrar oluşturulmaz. Başka konumdaki kısayol masaüstüne kopyalanır; uygulama, dosya veya klasör için `.lnk` oluşturulur. Kaynak dosya taşınmaz veya silinmez; isim çakışmasında yeni ad kullanılır. Yeni simgenin yerini Explorer belirler. Başka uygulamanın üzerine bırakma veya `Esc` ile iptal, gruptaki kaydı korur.
- Noktalı tutamaç tek panellerde de görünür ve tüm yüksekliği boyunca tutulabilir. Grupları birleştirmek için imlecin hedefin içine girmesi gerekmez: alanlar yeterince örtüşünce **Buraya bırakınca birleşir** işareti görünür.
- Masaüstü sağ tık → **WinDesk → Yeni grup** ile grup oluşturun; grup menüsündeki **Adı ve rengi düzenle** ile adını ve rengini değiştirin.
- Renk seçiminde seçili hazır renk, çevresindeki halka ve içindeki onay işaretiyle görünür. **Özel renk seç…** tam renk paletini açar; yanındaki alana `#RRGGBB` renk kodu da yazabilirsiniz. Özel renkler kaydedilir ve yeniden açılışta korunur. **Vazgeç** mevcut grubun rengini değiştirmez.
- Dört sütunda en fazla üç satır görünür. 12'den fazla kısayolda sağdaki ince dikey çubuk açılır; çubuğu sürükleyerek veya fare tekerleğiyle alt satırlara ulaşılır. Grup başlığı yerinde kalır.
- Büyük yönetim ekranı normal akıştan çıkarılmıştır. **Ayarlar** yalnızca kapanma süresi, sabitleme açıklaması, panel görünürlüğü ve masaüstü bağlantısını gösterir. Pencereyi kapatmak programı kapatmaz. Saatin yanındaki simgeye çift tıklayarak ayarlara ulaşın; sağ tık → **Çıkış** ile kapatın.

## Windows açılışında başlatma

WinDesk'in oturum açıldığında panelleri otomatik göstermesi için normal kullanıcı olarak `.\set-startup.ps1` çalıştırın. Bu komut mevcut kullanıcının Windows başlangıcına `dist\WinDesk\WinDesk.exe` dosyasını ekler; yönetici izni gerekmez. Windows masaüstü hazır olduğunda paneller mevcut düzenle görünür. Uygulama klasörünü taşırsanız komutu yeni konumdan tekrar çalıştırın.

Otomatik başlatmayı `.\set-startup.ps1 -Disable` veya **Windows Ayarları → Uygulamalar → Başlangıç → WinDesk** ile kapatabilirsiniz. Windows Ayarları'ndan daha önce devre dışı bıraktıysanız aynı yerden tekrar etkinleştirin.

## Geliştirme

Gerekli araç: [.NET 10 SDK](https://dotnet.microsoft.com/en-us/download/dotnet/10.0). WPF, Windows'a özgü yerel arayüz katmanıdır; tarayıcı veya Electron çalıştırılmaz.

```powershell
.\build.ps1 -Test
.\build.ps1 -Run
.\build-release.ps1 -Test
```

Bu çalışma alanındaki SDK `.tools\dotnet` altında izole tutulur. `build.ps1` önce onu, yoksa PATH üzerindeki SDK'yı kullanır. Sistem PATH'i değiştirilmez.

`build.ps1` geliştirme için `dist\WinDesk` altına .NET 10 Desktop Runtime gerektiren bir yayın çıkarır. `build-release.ps1` PATH üzerindeki .NET 10 SDK ile çalışma zamanını içeren tek EXE, ZIP, Türkçe Setup ve SHA-256 dosyasını `dist\releases\<sürüm>` altında üretir. Inno Setup 6.7.3 derleyicisi sabit SHA-256 değeriyle doğrulanan NuGet paketinden `.tools` altına çıkarılır; derleyicinin Pyrsys B.V. imzası kontrol edilir. Sistem geneline kurulum yapılmaz. Sürüm ana uygulamanın `.csproj` dosyasından alınır. Yerel geliştirme araçları, test verileri ve `.ua` analiz çıktıları Git deposuna alınmaz.

`tools\test-release.ps1` gerçek kurucuyu ayrı bir test klasöründe kurar, yayımlanan EXE'nin açılışını ve üç klasik menü komutunu doğrular, ardından test kurulumunu kaldırır. WinDesk'in kapalı olduğu ve kurucu/klasik menü kaydı bulunmayan bir test hesabı gerektirir. Mevcut kullanıcı düzeninin hash değerini kurulum öncesi ve sonrası karşılaştırır; günlükleri `artifacts` altında tutar.

### Windows uyumluluğunu doğrulama

Ana uygulama ve Setup için alt sınır Windows 10 22H2, derleme 19045'tir. Taşınabilir EXE de açılışta bu sınırı denetler. `net10.0-windows` ve mevcut Win32/WPF API'leri korunur; Windows 11'e özel modern menü isteğe bağlıdır ve kendi 22621 sınırını korur. `install-menu.ps1`, daha eski Windows sürümlerinde klasik menü kurulumuna yönlendiren bir hata verir.

Her hedef sistemde WinDesk kapalıyken `build-release.ps1 -Test` ve temiz bir test hesabında `tools\test-release.ps1` çalıştırılır. Tanılama raporu gerçek Windows sürümünü ve işlemci mimarisini kaydeder. Windows 10 testinde SDK kurmak istenmiyorsa, bu depoda oluşturulan yayın paketleriyle Setup/EXE açılışı ayrıca denenebilir. .NET'in Microsoft tarafından desteklenen Windows sürümleri, işletim sisteminin yaşam döngüsüne bağlıdır; uygulamanın çalışma uyumluluğu ayrı doğrulanır: [Microsoft destek tablosu](https://learn.microsoft.com/en-us/dotnet/core/install/windows#supported-versions).

| Ortam | Doğrulama |
| --- | --- |
| Windows 10 Enterprise Evaluation 22H2 x64 (19045.2006) | Docker içinde QEMU/KVM VM, 1280×800 / %100 DPI; global .NET olmadan ve yükseltilmemiş oturumda Setup/açılış/kaldırma, 55 çekirdek, 114 WPF/masaüstü ve 8 gerçek Explorer yeniden başlatma kontrolü geçti. |
| Windows 11 x64 (26200) | Bu geliştirme ortamında son düzeltmeyle 55 çekirdek ve 114 WPF/masaüstü kontrolü geçti; ayrıntılar RELEASE_NOTES.md içinde. |

VM kurulumu ve tekrar çalıştırma adımları [tools/vm/README.md](tools/vm/README.md) içindedir. Gerçek masaüstü ekran görüntüsü ve tanılama raporu alındı. Klasik menünün kayıtları ve uygulama komutları otomatik sınanır; Explorer menüsünde elle tıklama, oturum açılışında otomatik başlatma, sanal masaüstleri ve karma DPI monitörleri ayrıca doğrulanmalıdır. Normal testlerin bağlantı kaybı simülasyonuna ek olarak VM'de açıkça seçilen `--explorer-restart-test`, gerçek Explorer sürecini yeniden başlatır.

```text
src/WinDesk.Core/Workspace.cs       Paneller, kısayollar, JSON kayıt ve doğrulama
src/WinDesk.App/DesktopPanel.cs     Tek grup / akordiyon alanı, ortak konum ve açılma
src/WinDesk.App/PanelSection.*      Grup başlığı, kısayollar ve içerik
src/WinDesk.App/SettingsWindow.cs  Küçük genel ayarlar penceresi
src/WinDesk.App/PanelEditor.cs      Ad ve renk düzenleme
src/WinDesk.App/DesktopWindows.cs  Explorer bağlantısını izleme ve kurtarma
src/WinDesk.App/DesktopPanelHost.cs Masaüstüne bağlı yerel alt pencere
src/WinDesk.App/DesktopNative.cs   Explorer keşfi ve Win32 çağrıları
tests/WinDesk.Core.Tests/          Veri kaybını önlemeye yönelik kayıt testleri
tests/WinDesk.App.Tests/           WPF, canlı masaüstü bağlantısı ve akordiyon testleri
src/WinDesk.Shell/                 Windows 11 ilk sağ tık menüsü için yerel COM eklentisi
tests/WinDesk.Shell.Tests/         DLL ve kurulu COM kaydı kontrolleri
```

`-Test` kullanmadan önce çalışan WinDesk'i saat yanındaki menüden kapatın. Testler ayrı veri kullanır. Canlı Explorer altında alt pencere ilişkisini ve normal bir test penceresinin panelleri örtmesini kontrol eder; kısa süreli bir test penceresi açılır. Fare olayları ve akordiyon zamanlayıcıları program içinde tetiklenir. Explorer yeniden başlatılmaz: kaybolan alt pencere ve geçici bağlantı yokluğu ayrı ayrı simüle edilir. Projeyi Visual Studio'da açmak için `WinDesk.slnx` dosyasını kullanabilirsiniz.

Sürükleme testleri gerçek yerel panel pencereleri üzerinde sentetik işaretçi koordinatları kullanır; işletim sisteminin imleç altındaki pencere sonucu bu senaryoda enjekte edilir. Başka uygulamanın altında birleştirmeme kontrolü gerçek Windows sonucuyla ayrıca sınanır.

`--interactive-drag-test --quiet --data-dir <ayrı-test-klasörü>` test uygulamasında gerçek fare girdisi için bir pencere açar. Üretimle aynı başlık, tutamaç ve kısayol kontrollerini kullanır; yalnızca bu test penceresinin örtme sonucu masaüstüne eşlenir. Test kısayolları ayrı klasöre çıkarılır. Bu test, WPF fare yakalama ve olay sırasını kontrol eder; Explorer üzerinde doğrudan elle deneme yerine geçmez.

Gerçek masaüstü çizimini denetlemek için test projesi `--preview-desktop` ile açılabilir. Bu pencere Windows DWM'nin canlı masaüstü görüntüsünü gösterir; WPF'nin bellekte ürettiği örnek görsel değildir. Explorer'ı öne getirmez veya değiştirmez. `--inspect-desktop` ise pencere ilişkileri ve konumları için metin raporu verir.

## Veriler

Normal çalışmada `%LOCALAPPDATA%\WinDesk\workspace.json` kullanılır. Her kayıt önce geçici dosyaya yazılır, atomik olarak yer değiştirilir; bir önceki sürüm `.bak` dosyasında saklanır. Bozuk veya desteklenmeyen bir düzen okunursa üzerine yazmak yerine hata gösterilir.

0.7.4'te paket kimliği yalnızca menü eklentisinde tutulur; ana WPF uygulaması normal Win32 olarak çalışır. Menü, ana uygulamayı mevcut masaüstü Explorer süreci üzerinden başlatır; paket kimliği ana sürece aktarılmaz. `UserDataLocation` paket yönlendirmesi olmayan LocalAppData yolunu kullanır. Yanlışlıkla paket kimliğiyle açılırsa uygulama kayıt dosyasını okumadan durur. Okuma/erişim hataları boş ilk çalıştırma gibi değerlendirilmez.

Önceki geliştirme çalıştırmalarında kayıtlar Codex'in özel LocalCache alanına yönlenmişti. Normal kullanıcı dosyası yoksa eski kayıt doğrulanarak bir kez içe aktarılır; kaynak kopya korunur. Normal dosya varsa eski kayıtla üzerine yazılmaz. Ayrı boş kayıtlar kurtarma klasöründe korunmuştur. Geliştirme araçlarının kendi dosya yönlendirmesinden etkilenmemek için günlük kullanımda Explorer üzerinden veya kurulu WinDesk menüsüyle başlatın.

Sürüm 1–3 düzenleri okunurken sürüm 4'e yükseltilir; eski grup kimlikleri, kısayollar ve konumlar korunur. `Stacks` birleşik alanın adını ve ortak konumunu, her grubun `StackId` alanı üyeliğini, `IsPinned` alanı açık tutma tercihini tutar. Önceki düzenlerde gruplar varsayılan olarak sabitlenmemiştir. Birleştirme dosyaları bir klasörde toplamaz veya kısayolları tek listeye dönüştürmez.

0.7.5'ten itibaren `.lnk` içe aktarılırken gerçek hedef, başlatma parametreleri ve çalışma klasörü kaydedilir. Özel simge, yönetici olarak çalıştırma ve Windows kabuğu özelliklerini korumak için kısayolun bağımsız kopyası veri klasöründeki `Shortcuts` altında saklanır. `.url` dosyaları da bağımsız saklanır. Masaüstündeki kaynak kısayolu taşımak, yeniden adlandırmak veya silmek WinDesk kaydını bozmaz. Gerçek uygulama, dosya veya klasörün kendisi taşınırsa hedefin düzeltilmesi gerekir. Aynı uygulamanın farklı parametrelerle açılan profilleri ayrı öğeler olarak korunur.

Eski kayıtlar açılışta dönüştürülür. Kaynak kısayol kayıpsa Başlat menüsü ve masaüstündeki aynı adlı kısayollar kontrol edilir; yalnızca tek bir hedef/parametre eşleşmesi varsa onarılır. Belirsiz kayıtlar silinmez veya tahminle değiştirilmez. Geçiş öncesi düzen `workspace.json.before-shortcut-import-*.bak` dosyasında korunur. Dışarı sürükleme saklanan kısayolu tüm özellikleriyle dışarı kopyalar; asıl uygulama veya dosyayı taşımaz. Windows başlangıcına ekleme `set-startup.ps1` ile isteğe bağlı yapılır; mevcut masaüstü simgeleri otomatik gizlenmez.

## Masaüstü entegrasyonu

- Paneller `Window` yerine WPF içerik denetimleridir. Explorer masaüstünde **WS_CHILD | WS_EX_LAYERED** taşıyıcı oluşturulur ve alfa değeri 255 ile başlatılır. WPF, bu taşıyıcının içindeki normal `HwndSource` alt penceresine çizer. Windows 11 24H2'de doğrudan WPF piksel şeffaflığıyla görülen görünmez panel sorunu bu yolla giderilir. Arka plan opaktır; yuvarlatılmış kenarlar pencere bölgesiyle korunur.
- Explorer süreci doğrulanır; masaüstü simge görünümünü (`SHELLDLL_DefView`) barındıran `Progman` / `WorkerW` kökü bulunur. Panel bu kökün altında simge görünümüyle kardeştir; masaüstü simgelerini gizlemek paneli gizlemez. Explorer'ın stili veya pencere sırası değiştirilmez.
- Açılma, taşıma ve **Masaüstünde aç** işlemleri masaüstünü etkinleştirmez; `Topmost`, `Activate` veya sürekli arkaya gönderme kullanılmaz. Alt pencereler diğer uygulamaların üstüne çıkamaz.
- Explorer bağlantısı saniyede bir doğrulanır. Masaüstü yoksa paneller gizli kalır; uygun masaüstü geri geldiğinde kendi alt pencereleri yeniden oluşturulur. Hata halinde bağımsız pencereye dönüş yapılmaz. Kullanıcının **Panelleri gizle** tercihi bağlantı kurtarılırken korunur.
- DPI farkları için alt pencere oluşturulurken ana pencerenin DPI bağlamı geçici olarak kullanılır. Konumlar ekran koordinatından masaüstünün istemci koordinatına dönüştürülür.

## Kalan sınırlar

- `Progman` / `WorkerW` / `SHELLDLL_DefView` yerleşimi Windows kabuğunun sürüme bağlı iç yapısıdır. Desteklenmeyen kabukta program panelleri gizli tutar ve Ayarlar penceresinde bağlantı durumunu gösterir.
- Gerçek Explorer yeniden başlatılması Windows 10 VM'de görünür ve gizli panellerle doğrulandı. Windows 11'de gerçek yeniden başlatma, tüm sanal masaüstü geçişleri ve karma DPI monitör kombinasyonları ayrıca doğrulanmalıdır. Normal testler kullanıcının Explorer sürecini yeniden başlatmaz; VM testi ayrıca etkinleştirilir.
- Panel genişliği sabittir; dört sütun ve en fazla üç görünür satırdan sonra kaydırılır.
- Uygulama listesi tarama ve otomatik sınıflandırma sonraki geliştirmeler için bırakılmıştır.

Windows arayüzü için [Microsoft WPF belgeleri](https://learn.microsoft.com/en-us/dotnet/desktop/wpf/) temel alınmıştır.

Yerel pencere davranışı: [Win32 alt pencereler](https://learn.microsoft.com/en-us/windows/win32/winmsg/window-features#child-windows), [HwndSource ana pencere bağı](https://learn.microsoft.com/en-us/dotnet/api/system.windows.interop.hwndsourceparameters.parentwindow?view=windowsdesktop-10.0).

## Windows 11 sağ tık menüsü

Windows 11 22H2 ve üzerinde masaüstünün boş alanındaki ilk sağ tık menüsüne **WinDesk** alt menüsü kaydedilir. Komutlar: **Yeni grup**, **Panelleri gizle / göster**, **Ayarlar**. Uygulama kapalıysa başlatılır; açıksa komut mevcut örneğe kullanıcıya özel bir named pipe üzerinden iletilir. Yeni grup ve görünürlük komutları yönetim ekranını açmaz.

Yerel `IExplorerCommand` eklentisi bir COM surrogate içinde çalışır. `desktop10:ItemType` kaydı yalnızca `DesktopBackground` içindir. Explorer içine WPF/CLR yüklenmez. Paket yalnızca kimlik, ikon ve menü kaydı içerir; asıl uygulama `dist\WinDesk` dizininde kalır. Kurulumdan sonra bu klasörü taşımayın; taşınırsa menü kaydını yeniden kurun.

```powershell
.\build.ps1
.\build-menu.ps1 -Sign
# Bir defaya mahsus, yönetici onayıyla:
Start-Process pwsh -Verb RunAs -ArgumentList '-NoProfile -File "E:\WinDesk\tools\trust-menu-certificate.ps1"'
# Onay ve sertifika işlemi tamamlandıktan sonra normal kullanıcı olarak:
.\install-menu.ps1
.\artifacts\WinDesk.Shell.Tests.exe --registered
```

Derleme araçları `.tools` altında tutulur. Yerel geliştirme sertifikasının özel anahtarı kullanıcının kişisel sertifika deposunda, dışarı aktarılamaz olarak oluşturulur. Windows AppX kurulumu için yalnızca bu sertifikanın açık kısmı **LocalMachine\TrustedPeople** deposuna eklenir; bu tek seferlik işlem yönetici onayı gerektirir. Kök CA eklenmez ve Geliştirici Modu açılmaz. Menü paketi kullanıcının hesabına kurulur. Sertifika bir yıl geçerlidir; yeniden imzalama gerekirse yeni sertifika ayrıca güvenilir yapılmalıdır.

Menü kaydını kaldırmak için `.\uninstall-menu.ps1` kullanın. Gruplar ve uygulama dosyaları korunur. İmzalama sertifikasının sistemdeki güven kaydını da kaldırmak için `tools\trust-menu-certificate.ps1 -Remove` komutunu yönetici olarak çalıştırın. Explorer eski menüyü önbellekte tutarsa oturumu kapatıp açmak gerekebilir; kurulum Explorer'ı zorla yeniden başlatmaz.

Microsoft belgeleri: [Explorer menüsü ve paket kimliği](https://learn.microsoft.com/en-us/windows/apps/desktop/modernize/integrate-packaged-app-with-file-explorer), [DesktopBackground kaydı](https://learn.microsoft.com/en-us/uwp/schemas/appxpackage/uapmanifestschema/element-desktop10-itemtype).
