# WinDesk

Windows 11 için masaüstü grupları, akordiyon panelleri ve sürükle-bırak kısayolları. C# / WPF ile geliştirilmiştir.

## İndir

[En son sürümü indir](https://github.com/LogicalTR/WinDesk/releases/latest)

- **WinDesk-Setup-0.7.5-x64.exe:** Türkçe kurulum; masaüstü/Başlat kısayolları ve isteğe bağlı Windows başlangıcı.
- **WinDesk-0.7.5-win-x64.exe:** Kurulum gerektirmeyen tek dosyalı uygulama.
- **WinDesk-0.7.5-win-x64.zip:** Taşınabilir uygulama ve kullanım kılavuzu.

Windows 11 22H2 ve üzeri, x64 gerekir. .NET çalışma zamanı pakete dahildir. EXE dosyaları henüz Authenticode ile imzalı değildir; bütünlük kontrolü için sürümdeki SHA256SUMS.txt dosyasını kullanabilirsiniz.

## Kullanım

Saat yanındaki WinDesk simgesinden yeni grup ekleyin veya ayarlara ulaşın. Kısayolları gruplara bırakın; başlıkları sürükleyerek grupları birleştirin, sıralayın veya ayırın. Raptiye düğmesi grubu açık tutar.

Kurucunun masaüstü menüsü **Sağ tık → Daha fazla seçenek göster → WinDesk** altındadır. Yeni sürümü kurmadan önce WinDesk menüsünden **Çıkış**'ı seçin. Kaldırma ve güncelleme `%LOCALAPPDATA%\WinDesk` içindeki düzeni ve saklanan kısayolları silmez.

## Kaynak kodu

Bu depo yayınları dağıtır. Tam C# / WPF projesi, testler ve derleme betikleri her sürüme eklenen **WinDesk-0.7.5-source.zip** dosyasındadır. GitHub'ın otomatik **Source code** arşivleri yalnız bu dağıtım deposunu içerir; tam proje için ekli kaynak arşivini indirin.

0.7.5 dağıtımında 55 çekirdek ve 113 WPF/masaüstü kontrolü geçti. Kurulum, paketlenmiş uygulamanın açılışı, klasik menü komutları ve kaldırma ayrıca doğrulandı.
