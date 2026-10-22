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
