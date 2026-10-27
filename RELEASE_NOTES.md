# WinDesk 0.7.14

Ctrl + Shift'in zamanla çalışmayı bırakması ve masaüstüne dönen panel başlıklarının kaybolması düzeltildi.

- Kısayol dinleyicisi devre dışı kalırsa tuşlar bırakıldıktan sonra otomatik yenilenir. Kaçırılan tuş bırakma olayları temizlenir; uygulamayı yeniden başlatmak gerekmez. Sağ/sol Ctrl ve Shift desteklenir, Ctrl + Shift + T gibi ek tuşlu kısayollar paneli tetiklemez.
- Panelin görünen kısmına tıklamak o paneli geçici olarak öne getirir. Fare ayrıldıktan 2 saniye sonra masaüstüne döner; fareyle geri dönmek sayacı iptal eder. Menü, iletişim kutusu ve sürükleme sırasında panel önde kalır.
- Masaüstüne dönüşte görüntü yüzeyi yeniden oluşturulur. Başlıkların tamamen kaybolup ancak fareyle üzerine gelince görünmesi giderildi; konumlar, sabitlenmiş gruplar ve kısayollar korunur.
- Ctrl + Shift ile seçilen önde kalma modu fare ayrıldığında kapanmaz; aynı kısayol tekrar kullanıldığında masaüstüne döner.

**Güncelleme:** **Ayarlar → Güncellemeleri denetle** veya `WinDesk-Setup-0.7.14-x64.exe`. Mevcut düzen, gruplar ve saklanan kısayollar korunur.

Doğrulama: Windows 11 üzerinde 72 kısayol/kurtarma ve 287 masaüstü/arayüz kontrolü geçti. Gerçek Windows kısayol dinleyicisi testte kaldırılarak otomatik yeniden kurulması doğrulandı. Kurulu uygulamada sağ/sol Ctrl + Shift ile öne/arkaya geçiş ve ek tuşlu kısayolların tetiklememesi kontrol edildi.

Windows 10 22H2 sanal makinesinde 118 çekirdek, 72 kısayol/kurtarma ve 277 masaüstü kontrolü geçti. İlk çalışmada sabit 900 ms bekleyen bir kapanma kontrolü başarısız oldu; yalnız hata ayrıntıları eklenip davranış değiştirilmeden masaüstü paketi yeniden çalıştırıldığında geçti. Diğer 14 aşama ilk çalışmada geçti: gerçek kurulum, 0.7.13'ten güncelleme, uygulama içi güncelleme devri, kaldırma ve Explorer yeniden başlatması dahil. Düzen ve Unicode kısayol dosyalarının bayt düzeyinde korunduğu doğrulandı. Defender tehdit bulmadı; SmartScreen itibarı ve üretim imzası bu kontrollerin kapsamında değildir.

Bu sürümün EXE ve Setup dosyaları Authenticode ile imzalı değildir. SignPath Foundation kabulü ve üretim yapılandırması henüz tamamlanmadı. [İmzalama politikası](https://github.com/LogicalTR/WinDesk/blob/v0.7.14/CODE_SIGNING.md).

# WinDesk 0.7.13

Ctrl + Shift ile panelleri öne getirme, grup başlıklarının açılması ve doğru kullanıcı düzeninin korunması.

- **Ctrl + Shift** tuşlarına birlikte basıp bırakınca tüm paneller diğer pencerelerin önüne gelir; tekrar kullanınca masaüstüne geri döner. Konumlar ve raptiyeler korunur. Sağ/sol tuşlar desteklenir; Ctrl + Shift + T gibi ek tuşlu kısayollar bunu tetiklemez.
- İlk grup açıkken başka bir başlığa tıklamak bekleyen açılmayı iptal etmek yerine seçilen grubu doğrudan açar. Başlığı sürüklemek sıralama/ayırma işlevini korur. Fareyle üzerine gelme gecikmesi Ayarlar'daki süreyi kullanmaya devam eder.
- Paketlenmiş bir başlatıcı Windows dosya erişimini eski bir önbelleğe yönlendiriyorsa WinDesk düzeni okumadan önce normal Explorer bağlamında yeniden başlar. Böylece mevcut kısayolların eski bir düzen açıldığı için kaybolmuş görünmesi engellenir; kullanıcı dosyaları birleştirilmez veya değiştirilmez.
- 0.7.12'de hazırlanan ByBrowser indirme düzeltmeleri, isteğe bağlı indirme seçimi ve lisans/gizlilik bildirimleri dahildir.

**Güncelleme:** **Ayarlar → Güncellemeleri denetle** veya `WinDesk-Setup-0.7.13-x64.exe`. Gruplar ve saklanan kısayollar korunur.

Doğrulama: Windows 11 üzerinde 118 çekirdek, 252 WPF/masaüstü, 4 başlangıç bağlamı ve 39 imza/yayın kontrolü geçti. Windows 10 22H2 sanal makinesindeki 14 aşama geçti; aynı 118 çekirdek ve 252 masaüstü kontrolünün yanında gerçek kurulum, 0.7.6'dan 0.7.13'e güncelleme, uygulama içi güncelleme devri, kaldırma, kısayolların korunması ve Explorer yeniden başlatması sınandı. Defender paketlerde tehdit bulmadı. SmartScreen itibarı ve üretim imzası bu kontrollerin kapsamında değildir.

Bu sürümün EXE ve Setup dosyaları Authenticode ile imzalı değildir. SignPath Foundation kabulü ve üretim yapılandırması henüz tamamlanmadı. [İmzalama politikası](https://github.com/LogicalTR/WinDesk/blob/v0.7.13/CODE_SIGNING.md).

# WinDesk 0.7.12

MIT lisansı, gizlilik/üçüncü taraf bildirimleri ve GitHub üzerinde SignPath yayın hazırlığı eklendi. Uygulama, kaldırıcı ve Setup için iki aşamalı imzalama ve dosya bütünlüğü kontrolleri hazırlandı. SignPath kabulü ve ilk imzalı yayın henüz tamamlanmadı. [Code signing policy](https://github.com/LogicalTR/WinDesk/blob/main/CODE_SIGNING.md).

ByBrowser indirme seçeneği artık varsayılan olarak kapalıdır; kullanıcı seçtiğinde aşağıdaki indirme akışı çalışır. Kurulumda gizlilik bilgileri gösterilir ve paketlere lisans bildirimleri eklenir.

ByBrowser indirmesinin GitHub API istek sınırında durması düzeltildi.

- GitHub API 403 veya 429 döndürürse güncel kararlı sürüm resmi **Latest** yayın bağlantısından bulunur. Aynı sürümün `.sha256` dosyası, kurucu boyutu ve kurucusu doğrudan GitHub yayınından alınır; hesap veya token gerekmez.
- Tüm dosyalar bulunan sürüme sabitlenir. Yayın sırasında yeni bir sürüm çıksa bile doğrulama bilgisi ile kurucu karışmaz. Sürüm numarası uygulamaya sabitlenmez.
- Dosya adı, kaynak depo, kararlı sürüm, boyut ve SHA-256 kontrolleri korunur. İptal ve yeniden deneme çalışır; doğrulanmayan dosya çalıştırılmaz.
- Yayın dosyalarına erişim de 403/429 ile sınırlanırsa ham İngilizce HTTP hatası yerine Türkçe açıklama gösterilir.

Doğrulama: 118 çekirdek/indirme ve 19 arayüz kontrolü geçti. API yanıtı 403 olarak zorlanırken gerçek ByBrowser 2.24.0 kurucusunun 112.487.647 baytı resmi yayından indirildi ve SHA-256 doğrulaması geçti. Bu canlı kontrolde kurucu çalıştırılmadı; otomatik başlatma, bozuk dosyayı engelleme ve yeniden deneme arayüz testlerinde sınandı. Yerel EXE/ZIP/Setup paketleri hazırlandı. SignPath hazırlığından sonra Windows 10 22H2 sanal makinesindeki 13 aşama (kurulum, masaüstü, güncelleme, kaldırma ve Defender dahil) geçti. Windows 11 üzerinde 118 çekirdek, 19 güncelleme arayüzü, 18 kapanma süresi ve 39 imza/yayın kontrolü geçti; Defender tehdit bulmadı. Gerçek EXE/kaldırıcı/Setup imza gömme provası yalnız bellekte üretilen güvenilmeyen test anahtarıyla yapıldı ve üretim doğrulayıcısı bu imzayı reddetti. Workflow sözdizimi ve SignPath XML şemaları doğrulandı. Canlı GitHub/SignPath üretim imzalama, ilk hizmet metadata kabulü ve imzalı dosyayla SmartScreen testi henüz yapılmadı.

# WinDesk 0.7.11

Kurulum sonunda ByBrowser'ın en güncel sürümünü doğrudan indirme ve kurulum sihirbazını otomatik açma.

- Son ekrana **ByBrowser'ı da indirmek ister misiniz? (İndir ve kurulumu başlat)** seçeneği eklendi. Varsayılan olarak seçilidir; istemeyen kullanıcı işareti kaldırabilir.
- **Bitti**'ye basıldığında ByBrowser'ın resmi GitHub yayınındaki en güncel kararlı x64 kurucu bulunur ve doğrudan indirilir. Tarayıcıda indirme sayfası açılmaz; sürüm numarası sabitlenmez.
- İlerleme penceresi indirme durumunu gösterir; iptal ve hata sonrası yeniden deneme desteklenir. Dosyanın boyutu ve SHA-256 değeri doğrulanınca ByBrowser kurulum sihirbazı otomatik açılır. İndirme bozuksa kurucu çalıştırılmaz. Kurulum sihirbazı kendi normal adımlarıyla devam eder.
- İndirilen dosyaya **Dosyayı göster** ile ulaşılabilir. Dosyalar `%LOCALAPPDATA%\WinDesk\Downloads\ByBrowser` altında saklanır. Sessiz WinDesk kurulumlarında ByBrowser indirilmez ve kurucusu başlatılmaz.
- İndirme penceresi çalışan WinDesk'ten bağımsızdır; kayıtlı panel düzenini açmaz. Önceki sürümlerdeki kapanma süresi ve uygulama simgesi düzeltmeleri dahildir.
- WinDesk güncellemesinde dosya doğrulama hataları da pencereyi kapatmadan yeniden indirme olanağı sunar.

**Güncelleme:** **Ayarlar → Güncellemeleri denetle** veya `WinDesk-Setup-0.7.11-x64.exe` kullanılabilir.

Doğrulama: 95 çekirdek/indirme ve 16 arayüz kontrolü geçti. Güncel sürümün değişmesi, hatalı yayın bilgisi, ağ hatası, iptal, bozuk dosya ve kurulum öncesi yeniden doğrulama sınandı. Windows 11'de kurulumun son ekranı, varsayılan seçim ve sessiz kurulum davranışı dosya/kayıt değişiklikleri kapatılmış bir Inno Setup önizlemesiyle kontrol edildi; gerçek ByBrowser 2.24.0 kurucusu indirildi, GitHub boyut/SHA-256 değerleriyle karşılaştırıldı ve kurulum sihirbazının otomatik açıldığı doğrulandı. Çalışan WinDesk düzeninin değişmediği kontrol edildi. Bu sürüm için tam kurulum/kaldırma ve Windows 10 VM testleri tekrarlanmadı.

Dağıtım: Windows 10 22H2 (19045) ve üzeri x64; .NET çalışma zamanı dahildir. Türkçe Setup, taşınabilir EXE/ZIP, tam kaynak kodu arşivi ve `SHA256SUMS.txt` birlikte sunulur. EXE dosyaları henüz Authenticode ile imzalı değildir.

# WinDesk 0.7.10

Birleşik gruplar arasında geçiş artık seçilen otomatik kapanma süresini kullanır.

- Başka grup başlığına gelindiğinde mevcut sabitlenmemiş grup, **Ayarlar → Otomatik kapanma** süresi dolana kadar açık kalır. Ardından mevcut grup kapanır ve yeni grup açılır. Örneğin 1,50 saniye seçildiğinde geçiş de bu süreyi bekler.
- Beklerken önceki gruba geri dönmek geçişi iptal eder. Panelin tamamından çıkmak bekleyen geçişi iptal eder ve normal otomatik kapanma süresini başlatır. Başlıklar arasında hızlı hareket edildiğinde yalnız son hedef açılır.
- Süre ayarını değiştirmek bekleyen grup geçişine de uygulanır. İlk grubu açmak ve sabitlenmiş bir grubun yanındaki grubu açmak hızlı kalır; raptiyeyle sabitlenmiş gruplar açık kalır.
- Sağ tık menüsü veya düzenleme penceresi açıldığında bekleyen grup geçişi iptal edilir. Ayarlardaki açıklama bu davranışı anlatacak şekilde güncellendi.
- 0.7.9'daki uygulama güncellemesi sonrası simge düzeltmesi dahildir. Kayıtlı panel düzeni, kısayollar ve mevcut süre tercihi korunur.

**Güncelleme:** 0.7.7 ve sonraki sürümlerde **Ayarlar → Güncellemeleri denetle** kullanılabilir. Daha eski sürümlerde `WinDesk-Setup-0.7.10-x64.exe` dosyasını indirip WinDesk'i kapatarak kurun.

Doğrulama: Windows 11 üzerinde gerçek WPF zamanlayıcılarıyla 18 kapanma ve grup geçişi kontrolü geçti. 0,20 / 1,50 / 2,00 saniye ayarları, geri dönüşte iptal, panelden ayrılma, beklerken süre değişikliği, hızlı başlık geçişleri, sabitleme ve menü/pencere etkileşimleri sınandı. Bu kontroller yayın akışına da eklendi. Yerel düzeltme kurulu uygulamaya uygulanıp yeniden açıldı; kayıtlı düzenin değişmediği doğrulandı. Bu düzeltme için Windows 10 VM ve tam masaüstü etkileşim testleri tekrarlanmadı.

Dağıtım: Windows 10 22H2 (19045) ve üzeri x64; .NET çalışma zamanı dahildir. Türkçe Setup, taşınabilir EXE/ZIP, tam kaynak kodu arşivi ve `SHA256SUMS.txt` birlikte sunulur. EXE dosyaları henüz Authenticode ile imzalı değildir.

# WinDesk 0.7.9

Uygulama güncellemelerinden sonra kaybolan kısayol simgeleri için kalıcı düzeltme.

- Bir uygulama güncellemesi eski sürüme ait özel simge dosyasını silerse WinDesk artık uygulamanın kendi simgesini gösterir. Örneğin ByBrowser güncellemesi sonrasında boş dosya simgesi görünmez; kısayolu panelden kaldırıp yeniden eklemek gerekmez.
- Mevcut özel simgeler korunur. Simge dosyası yeniden kullanılabilir olduğunda sonraki simge yüklemesinde tekrar tercih edilir.
- Kısayol dosyaları, açılış parametreleri, çalışma klasörü ve panel düzeni değiştirilmez. Ortam değişkeni içeren simge yolları ve eski kısayol kayıtları da desteklenir.
- Uygulamanın kendisi de eksikse veya kısayol okunamıyorsa Windows'un normal simge davranışı korunur; diğer panel öğelerinin yüklenmesi engellenmez.

**Güncelleme:** 0.7.7 ve 0.7.8 kullanıcıları **Ayarlar → Güncellemeleri denetle** ile güncelleyebilir. Daha eski sürümlerde `WinDesk-Setup-0.7.9-x64.exe` dosyasını indirip WinDesk'i kapatarak kurun.

Doğrulama: Windows 11 üzerinde 26 kısayol içe aktarma/dışa aktarma ve simge kontrolü geçti. Güncelleme taklidinde özel simge dosyası silinerek gösterilen simgenin uygulamanın gerçek simgesiyle piksel düzeyinde eşleştiği doğrulandı. Geçerli özel simgeler, ortam değişkenli yollar, eksik hedefler, bozuk kısayollar ve açılış bilgilerinin korunması sınandı. Günlük kullanılan uygulama yeniden açıldı; kayıtlı düzenin değişmediği doğrulandı. Bu düzeltme için Windows 10 VM ve tam masaüstü etkileşim testleri tekrarlanmadı.

Dağıtım: Windows 10 22H2 (19045) ve üzeri x64; .NET çalışma zamanı dahildir. Türkçe Setup, taşınabilir EXE/ZIP, tam kaynak kodu arşivi ve `SHA256SUMS.txt` birlikte sunulur. EXE dosyaları henüz Authenticode ile imzalı değildir.

# WinDesk 0.7.8

Çoklu ekranda panel konumunun yeniden açılışta korunması.

- İkinci ekrana taşınan panel veya birleşik alanın kayıtlı konumu, açılışta kullanılan geçici görünür konumla artık değiştirilmez.
- Ekran geç algılanırsa veya masaüstü bağlantısı yeniden kurulursa alanın tercih edilen yeri korunur; ekran hazır olduğunda alan kayıtlı konumuna döner.
- Paneli fareyle yeniden taşımanız yeni konumu kaydeder. Mevcut gruplar, kısayollar, sabitleme ve diğer ayarlar korunur; düzen dosyası biçimi değişmez.
- 0.7.7 ve önceki sürümlerdeki uygulama içinden güncelleme, Türkçe/Unicode kısayol ve Windows 10 uyumluluk düzeltmeleri bu sürüme dahildir.

**Güncelleme:** 0.7.7 kullanıcıları **Ayarlar → Güncellemeleri denetle** ile güncelleyebilir. Daha eski sürümlerde `WinDesk-Setup-0.7.8-x64.exe` dosyasını indirip WinDesk'i kapatarak kurun.

Doğrulama: Windows 11 üzerinde 81 çekirdek/güncelleme ve 139 WPF/masaüstü kontrolü geçti. İki gerçek ekranın sağ kenarında kaydetme/yükleme, tek panel ve birleşik alan, masaüstüne yeniden bağlanma, ekranın geç algılanmasının taklidi ve geçici konumun kayıtlı yerin üzerine yazılmaması sınandı. Günlük kullanılan uygulama yeniden açılarak ikinci ekranın en sağındaki konumu ve kullanıcı düzeninin değişmediği doğrulandı. Bilgisayar yeniden başlatılmadı; bu sürüm için Windows 10 VM ve karma DPI doğrulaması tekrarlanmadı.

Dağıtım: Windows 10 22H2 (19045) ve üzeri x64; .NET çalışma zamanı dahildir. Türkçe Setup, taşınabilir EXE/ZIP, tam kaynak kodu arşivi ve `SHA256SUMS.txt` birlikte sunulur. EXE dosyaları henüz Authenticode ile imzalı değildir.

# WinDesk 0.7.7

Uygulama içinden güncelleme ve tek komutla arka planda yayın.

- **Ayarlar → Güncellemeleri denetle** yeni kararlı GitHub sürümünü bulur. İndirme arka planda, ilerleme göstergesi ve iptal seçeneğiyle yapılır.
- Kurucu boyutu ve SHA-256 değeri doğrulanır; eksik, bozuk veya indirmeden sonra değiştirilmiş dosya çalıştırılmaz. Kurulum yalnız kullanıcının onayıyla açılır.
- Kurulumdan önce düzen kaydedilir ve WinDesk normal şekilde kapanır. Kurucu kapanmayı bekler; kullanıcı verileri korunur. Taşınabilir uygulamada bu seçenek kullanıcı hesabına kurulum yapar.
- Geliştirici için `publish-release.ps1`: test, EXE/ZIP/Setup ve kaynak arşivi, GitHub taslağı, yükleme doğrulaması, yayın ve indirme bağlantılarının güncellenmesi. Varsayılan olarak gizli bir arka plan işlemi çalışır; günlük ve durum dosyası üretir. Kesilen yükleme aynı komutla sürdürülür.

**0.7.6 ve daha eski sürümlerden geçiş:** Güncelleme düğmesi eski uygulamada bulunmaz. `WinDesk-Setup-0.7.7-x64.exe` dosyasını bir kez indirip WinDesk'i kapatarak kurun; sonraki sürümleri uygulama içinden denetleyebilirsiniz.

Windows 10 22H2 (19045) ve üzeri x64; .NET çalışma zamanı dahildir. EXE dosyaları henüz Authenticode ile imzalı değildir. Kaynak kodu ekli `WinDesk-0.7.7-source.zip` dosyasındadır; GitHub'ın otomatik Source code arşivleri dağıtım deposunu içerir.

Doğrulama: 81 çekirdek/güncelleme, 13 yayın doğrulama ve 7 pencere açmadan çalışan güncelleme arayüzü kontrolü. Windows 10 VM paketi ayrıca gerçek 0.7.6 → 0.7.7 kurulum geçişini; özel kurulum klasörü, başlangıç seçimi, menü komutları, gruplar ve saklanan kısayolların korunmasını sınar. Uygulamanın indirme doğrulaması, kaydetme/kapanma ve gerçek kurucuyu başlatma akışı da VM'de denenir; henüz yayımlanmamış sürüm yanıtı yerel test verisinden gelir ve gerçek kurucu sihirbazı belgelenmiş sessiz parametrelerle tamamlanır. Test günlükleri ve paket hash'leri VM raporunda tutulur. Aşağıdaki tarihli Windows 10/11 masaüstü sonuçları 0.7.6'ya aittir.

# WinDesk 0.7.6

Windows 10 22H2 (19045) ve üzeri x64 sistemleri hedefleyen uyumluluk güncellemesi.

- Setup'ın minimum Windows sürümü 22621'den 19045'e indirildi.
- Taşınabilir EXE, desteklenen minimum sürümü kullanıcı verilerini açmadan ve masaüstü panellerini oluşturmadan denetler.
- Klasik sağ tık menüsü Windows 10 ve 11 için korunur; kurulum ve kullanım metinleri iki sisteme göre güncellendi.
- İsteğe bağlı modern menü eklentisi Windows 11 22H2 gerektirmeye devam eder. Kurulum betiği eski sistemlerde klasik menüye yönlendirir.
- Ayarlar penceresi sürüm bilgisini derlenen uygulamadan okur. Tanılama çıktısı gerçek Windows sürümünü ve işlem mimarisini içerir.
- İngilizce Windows 10 VM testinde bulunan Türkçe kısayol parametresi kaybı düzeltildi. Kısayol okuma ve oluşturma Unicode `IShellLinkW` kullanır; özgün `.lnk` kopyaları korunur. Türkçe/Japonca çalışma klasörleri ve dışa aktarılan kısayolun gerçek başlatma parametreleri sınanır.

Dağıtım paketleri: `WinDesk-Setup-0.7.6-x64.exe`, taşınabilir EXE, ZIP, tam kaynak kodu arşivi ve `SHA256SUMS.txt`. .NET çalışma zamanı dahildir. Sürüm sayfası: [WinDesk 0.7.6](https://github.com/LogicalTR/WinDesk/releases/tag/v0.7.6).

22 Ekim 2026 doğrulaması, Windows 11 x64 (10.0.26200.0):

- `build-release.ps1 -Test`: 55 çekirdek, 113 WPF/masaüstü kontrolü; ayrıca 14 kısayol içe aktarma ve 4 kayıt kurtarma kontrolü geçti.
- `tools\test-release.ps1`: gerçek Setup kurulumu, paketli EXE'nin ayrı veriyle açılıp panelleri göstermesi, üç klasik menü komutunun kaydı ve kaldırmada mevcut düzenin korunması geçti.
- Yayın dosyalarının SHA-256 değerleri ve ZIP içeriği doğrulandı.
- VM'de bulunan Unicode sorunu düzeltildikten sonra Windows 11'de `build.ps1 -Test`: 55 çekirdek ve 114 WPF/masaüstü kontrolü geçti. Ayrı kısayol testinde 15 kontrol geçti. Günlük kullanım uygulaması yeniden derlenip açıldı; mevcut kullanıcı düzeninin SHA-256 değeri değişmedi.

22 Ekim 2026 Windows 10 VM doğrulaması (`run-004`):

- Gerçek Windows 10 Enterprise Evaluation 22H2 x64, **19045.2006**, QEMU/KVM; 4 vCPU, 6 GB RAM, 1280×800 / %100 DPI. Global .NET/SDK kurulu değil; test işlemi yönetici olarak yükseltilmedi.
- Son 0.7.6 Setup kurulumu, yayımlanan EXE ile hash eşitliği, üç klasik menü kaydı, ayrı veriyle panel açılışı ve kaldırma geçti.
- 55 çekirdek ve **114 WPF/masaüstü kontrolü** geçti. Ayrı çalıştırmalarda 15 kısayol içe aktarma/dışa aktarma, 4 kayıt kurtarma ve 7 görünüm kontrolü de geçti; bunlar 114 kontrolün içindeki senaryoların ayrıca çalıştırılmasıdır.
- Gerçek Explorer süreci iki kez yeniden başlatıldı: görünür ve gizli panellerde pencerenin yeniden oluşturulması, yeni masaüstüne bağlanma, görünürlük tercihi, konum ve sabitleme için **8 kontrol** geçti.
- Windows 11 modern menü kurulumunun Windows 10'da yan etki oluşturmadan reddedilmesi geçti. Gerçek Windows 10 masaüstü ekran görüntüsünde iki panelin çizildiği görüldü.
- VM'de ortaya çıkan test varsayımları da düzeltildi: 1280×800 ekran için sürükleme hedefi, sonuç dosyasının atomik yayımlanması, PowerShell 5.1 çıkış kodu okuma ve Samba üzerinden rapor/istek aktarımı.
- Sonuç ve günlükler: `artifacts/windows10-22h2-0.7.6/run-004/`; EXE SHA-256: `7f2cb45632eae2612dbc47e4d4d49175d7cbf9522d160aa64bf3b07fc4e2283e`.

VM kullanıcı isteğiyle kapatılıp `C:\Users\Burak\WinDesk-VMs\win10-22h2` altında saklanır. Yeniden açma adımları `tools/vm/README.md` içindedir. Değerlendirme imajı etkinleştirme uyarısı gösteriyor (`LicenseStatus=5`, kalan süre 0); bu kurulum sınırsız süreli lisanslı bir Windows değildir.

19045'ten eski gerçek Windows üzerinde minimum sürüm uyarısı, Windows 11'de gerçek Explorer yeniden başlatması, sanal masaüstü geçişleri, oturum açılışında otomatik başlatma ve karma DPI/fiziksel monitör senaryoları ayrıca denenmelidir. Menü kayıt kontrolleri Explorer menüsünün elle tıklanarak denenmesi anlamına gelmez.

# WinDesk 0.7.5

Windows 11 için ilk indirilebilir WinDesk dağıtımı.

- Masaüstüne bağlı gruplar, sürükle-bırak kısayolları ve birleşik akordiyon alanları.
- Grup sabitleme, sıralama, özel renkler ve ayarlanabilir otomatik kapanma.
- Kaynak kısayol silinse bile özelliklerini koruyan bağımsız kısayol kopyaları.
- Türkçe, kullanıcı hesabına kurulum; masaüstü/Başlat kısayolları, isteğe bağlı klasik sağ tık menüsü ve Windows başlangıcı.
- .NET çalışma zamanı dahil tek dosyalı taşınabilir EXE.

**Önerilen indirme:** `WinDesk-Setup-0.7.5-x64.exe`.
Kurulum istemiyorsanız `WinDesk-0.7.5-win-x64.exe` dosyasını doğrudan açın veya ZIP paketini çıkarın.

Windows 11 22H2 ve üzeri, x64 gerekir. Ayrıca .NET kurmanız gerekmez.
Güncellemeden önce çalışan WinDesk'i saat yanındaki menüden kapatın.
Gruplar ve kısayollar `%LOCALAPPDATA%\WinDesk` altında korunur; kaldırma bu verileri silmez.

Kurucunun masaüstü menüsü **Daha fazla seçenek göster → WinDesk** altında görünür.
Windows 11'in ilk menüsüne eklenen geliştirme MSIX paketi bu dağıtıma dahil değildir.
Otomatik güncelleme ve başka bir uygulama kurma teklifi bu sürümde yoktur.
EXE dosyaları henüz Authenticode ile imzalı değildir. Dosya bütünlüğü için `SHA256SUMS.txt` sağlanır.

Doğrulama: 55 çekirdek ve 113 WPF/masaüstü kontrolü geçti. Gerçek Setup kurulumu, paketlenmiş EXE'nin ayrı veriyle açılışı, üç klasik menü komutu ve kaldırmada mevcut düzenin korunması doğrulandı. .NET bulunmayan ayrı bir bilgisayarda deneme yapılmadı.
