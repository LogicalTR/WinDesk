# SignPath yayın hazırlığı

Seçilen yol: ücretsiz SignPath Foundation, WinDesk kaynakları için MIT lisansı.
Entegrasyon hazırlandı; hesap kabulü, ilk hizmet imzası ve GitHub üzerinde canlı
iş akışı henüz doğrulanmadı. Mevcut v0.7.11 dosyaları imzasız kalır.
[Code signing policy](../CODE_SIGNING.md) ve [Foundation koşulları](https://signpath.org/terms.html).

## İlk kurulum

1. Bu kaynak ağacını, testleri, `LICENSE`, politika dosyalarını ve `.github`
   dizinini public `LogicalTR/WinDesk` deposuna PR olarak taşıyıp inceleyin.
   22 Ekim 2026 kontrolünde uzak `main` yalnız README içeriyordu. Kaynak ZIP'i
   tek başına GitHub kaynak derlemesi için yeterli değildir. Dağıtım README'sini
   kaynak README'siyle değiştirin; eski sürümler korunabilir.
2. GitHub ve SignPath hesaplarında MFA'yı açın. `main` için force push/silme
   koruması ve dış katkılara PR incelemesi ayarlayın. `CODEOWNERS` tek başına
   branch protection oluşturmaz.
3. [SignPath Foundation başvurusunu](https://signpath.org/apply.html) proje sahibi
   olarak tamamlayın. [Başvuru taslağı](signpath/APPLICATION.md) hazırdır.
   Kurum, proje geçmişi/itibarı dahil başvuruyu ayrıca değerlendirir; ücretsiz
   sertifika otomatik hak değildir. Kabul veya sponsor ilişkisi henüz yoktur.
4. Kabul sonrası SignPath'te GitHub.com Trusted Build System'i ekleyin ve
   `WinDesk` projesini public depoya bağlayın. [SignPath GitHub App](https://github.com/apps/signpath)
   erişimini yalnız gereken depoya verin. [Resmî entegrasyon](https://docs.signpath.io/trusted-build-systems/github).
5. `components` ve `installer` artifact configuration'larını
   [components.xml](signpath/components.xml) ve [installer.xml](signpath/installer.xml)
   ile oluşturun. Dosya yollarını ve ürün kısıtlarını genişletmeyin.
6. `release-signing` politikasını Foundation üretim sertifikasına bağlayın:
   SHA-256, zaman damgası, GitHub kaynak doğrulaması, GitHub-hosted runner,
   yalnız `LogicalTR/WinDesk`, `main` ve `.github/workflows/signpath-release.yml`.
   Her isteğe manuel onay zorunlu olsun; onaylayıcı LogicalTR. CI token'ı yalnız
   submitter yetkisi taşımalı, onay yetkisi taşımamalıdır. Bu kısıtları sağlayıcı
   panelinde de ayarlayın; YAML koşulu servis politikasının yerine geçmez.
7. GitHub'da `signpath-production` environment oluşturun, yalnız `main` için
   deployment izni ve maintainer review ayarlayın. Şu alanları buraya ekleyin:

   | Tür | Ad | Değer |
   | --- | --- | --- |
   | Secret | `SIGNPATH_API_TOKEN` | Yalnız submitter token'ı |
   | Variable | `SIGNPATH_ORGANIZATION_ID` | SignPath kuruluş ID'si |
   | Variable | `SIGNPATH_CERTIFICATE_SHA1` | Üretim sertifikasının 40 haneli parmak izi |

   Token/özel anahtar/parola sohbet veya Git dosyalarına yazılmaz. Sertifika
   yenilenince parmak izi doğrulanarak güncellenir. Workflow'daki proje/politika
   slug'ları bu adlarla eşleşmelidir. Test sertifikası üretim doğrulamasını
   geçmemelidir; güvenilirlik kontrollerini kapatmayın.

## Derleme ve imzalama

`Build and test` PR'larda imzasız derleme/test yapar; secret kullanmaz.
`SignPath release` yalnız manuel olarak `main` üzerinde başlatılır. Başka commit,
dosya URL'si veya hazır EXE girdisi almaz. Action'lar tam commit SHA'sına, SDK
`global.json` ile 10.0.402'ye, runtime 10.0.13'e, Inno paketi hash ile 6.7.3'e
sabitlenmiştir. NuGet kaynağı `NuGet.Config` ile sınırlandırılır. Bu sabitlemeler
yeni sürümlerde birlikte güncellenmelidir; zaman damgalı paketlerin bit düzeyinde
tekrar üretileceği iddia edilmez.

1. Çekirdek, güncelleme arayüzü ve imza bütünlüğü testleri çalışır.
2. `tools/signpath-release.ps1 -Stage Prepare` kaynak kodu derler ve Inno'nun
   [offline imzalama](https://jrsoftware.org/ishelp/topic_setup_signeduninstaller.htm)
   desteğiyle kaldırıcı/geçici kurulum runtime'ını çıkarır. İlk Inno derlemesinin
   imza isteyerek durması beklenir; başka hata kabul edilmez.
3. GitHub artifact'ına yüklenen `app/WinDesk.exe` ve
   `installer/WinDesk.Uninstaller.exe` ilk SignPath isteğine gider. Onay sonrası
   sertifika, zaman damgası, metadata ve imza alanları dışındaki bütün baytlar
   doğrulanır. Microsoft DLL'leri yeniden imzalanmaz.
4. `Assemble` imzalı uygulamayı paketler. Inno kaldırıcı girdisini tekrar üretir,
   imzalı önbellekle karşılaştırır ve imzayı kurucuya gömer. Son Setup ikinci
   GitHub artifact'ı ve ikinci manuel SignPath onayıyla imzalanır. Yalnız dış
   Setup'ı imzalamak yeterli sayılmaz.
5. `Complete` EXE, ZIP, Setup, aynı commit'in kaynak ZIP'i ve son baytlardan
   `SHA256SUMS.txt` üretir. ZIP ve kurulum lisans/gizlilik dosyalarını içerir;
   tek EXE'de lisanslar .NET kaynakları olarak da gömülüdür.
6. Geçici GitHub runner'ında kurulum/kaldırma, payload, menü kayıtları, lisanslar
   ve kurulu kaldırıcı imzası test edilir (`-InstallerOnly`). Bu kontrol Explorer
   etkileşimli testlerinin yerine geçmez. Sonuçlar GitHub artifact'ı olarak
   incelemeye sunulur; otomatik yayın yapılmaz.

Inno 6.7.3 metadata metinlerini sabit uzunlukta boşluklarla doldurur; yerel
kontrol yalnız sondaki boşlukları normalleştirir. İlk hizmet provasında **her iki
XML'in gerçek PE metadata kabulü** de doğrulanmalıdır. Sağlayıcı farklı eşleştirme
isterse kısıtları kaldırmadan desteklenen ayarı onunla netleştirin. Yerel XSD
doğrulaması hizmet kabulü değildir.

## Yayın

İki isteğin onayını, GitHub işinin başarısını ve kaynak commit'ini inceleyin.
`WinDesk-<sürüm>-signed-release` artifact'ını indirin; bu EXE'leri değiştirmeyin.
Windows'ta `Assert-ReleasePackageSignatures` ile imzaları tekrar kontrol edin,
`SHA256SUMS.txt` ile dört dosyayı karşılaştırın. Temiz Windows 10/11 hesabında tam
testleri tamamlayın. Sürüm etiketini **manifestteki kaynak commit'ine** koyun,
beş dosyayı taslak Release'e ekleyin ve sürüm notlarında
[Code signing policy](https://github.com/LogicalTR/WinDesk/blob/main/CODE_SIGNING.md)
bağlantısını koruyun. Sunucudaki hash/boyutları doğrulayıp taslağı yayımlayın.

Eski `publish-release.ps1` yerel yayın yolu artık yayın yapmaz; yanlış commit'e
etiket veya yerel binary yüklenmesini önlemek için durur. `-ValidateOnly` eski
CA tabanlı kontrol için tutulmuştur; SignPath kaynak derlemesi sayılmaz.
`build-release.ps1` yerel CA sertifikasıyla veya `-Unsigned` ile derleyebilir;
SignPath bu bilgisayarın sertifika deposunu kullanmaz.

## Yerel doğrulama

```powershell
dotnet run --project tests/WinDesk.Core.Tests -c Release
./tests/Test-ReleaseSigning.ps1
./tests/Test-PublishRelease.ps1
./tests/Test-SignPath.ps1
./build-release.ps1 -Unsigned
# Yeni, henüz var olmayan bir çalışma klasörü seçin:
./tools/signpath-release.ps1 -Stage Prepare -WorkDirectory artifacts/signpath-local
./tests/Test-InnoSigningRoundtrip.ps1 -PreparedDirectory artifacts/signpath-local
# Yalnız temiz test hesabında veya Windows VM'de:
./tools/test-release.ps1 -ReleaseDirectory dist/unsigned/0.7.12
./tools/test-windows-security.ps1 -ReleaseDirectory dist/unsigned/0.7.12 `
    -ReportDirectory artifacts/security-local -ScanDefender
# Gerçek imzalı paket alındıktan sonra:
./tools/test-release.ps1 -ReleaseDirectory '<signed-release>' -RequireSignature
```

Defender, Authenticode güveni ve SmartScreen itibarı ayrı sonuçlardır.
[Microsoft'un açıklaması](https://learn.microsoft.com/en-us/windows/apps/package-and-deploy/code-signing-options).
SmartScreen testi temiz oturumda internetten indirilmiş dosyayla yapılır; sessiz
kurulum bu test sayılmaz. `Zone.Identifier` korunmalı, uyarı, yayıncı, hash ve
Windows sürümü kaydedilmelidir. Güvenlik korumalarını kapatmayın.

`Test-InnoSigningRoundtrip` yalnız bellekte oluşturulan, hiçbir sertifika
deposuna eklenmeyen ve güvenilmeyen test anahtarıyla gerçek PE'leri imzalar.
Inno'nun bu imzayı gömmesini ve üretim doğrulayıcısının test imzasını reddetmesini
sınar. Bu, canlı SignPath veya Windows tarafından güvenilen yayın imzası değildir.
