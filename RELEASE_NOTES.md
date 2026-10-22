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
