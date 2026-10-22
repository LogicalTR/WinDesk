# WinDesk

Windows 10 ve 11 için masaüstü grupları, akordiyon panelleri ve sürükle-bırak kısayolları. C# / WPF ile geliştirilmiştir.

## İndir

[En son sürümü indir](https://github.com/LogicalTR/WinDesk/releases/latest)

- [WinDesk-Setup-0.7.6-x64.exe](https://github.com/LogicalTR/WinDesk/releases/download/v0.7.6/WinDesk-Setup-0.7.6-x64.exe): Türkçe kurulum; masaüstü/Başlat kısayolları ve isteğe bağlı Windows başlangıcı.
- [WinDesk-0.7.6-win-x64.exe](https://github.com/LogicalTR/WinDesk/releases/download/v0.7.6/WinDesk-0.7.6-win-x64.exe): Kurulum gerektirmeyen tek dosyalı uygulama.
- [WinDesk-0.7.6-win-x64.zip](https://github.com/LogicalTR/WinDesk/releases/download/v0.7.6/WinDesk-0.7.6-win-x64.zip): Taşınabilir uygulama ve kullanım kılavuzu.

Windows 10 22H2 (19045) ve üzeri, x64 gerekir. .NET çalışma zamanı pakete dahildir. EXE dosyaları henüz Authenticode ile imzalı değildir; bütünlük kontrolü için sürümdeki SHA256SUMS.txt dosyasını kullanabilirsiniz.

## Kullanım

Saat yanındaki WinDesk simgesinden yeni grup ekleyin veya ayarlara ulaşın. Kısayolları gruplara bırakın; başlıkları sürükleyerek grupları birleştirin, sıralayın veya ayırın. Raptiye düğmesi grubu açık tutar.

Kurucunun masaüstü menüsü Windows 10'da **Sağ tık → WinDesk**, Windows 11'de **Sağ tık → Daha fazla seçenek göster → WinDesk** altındadır. Yeni sürümü kurmadan önce WinDesk menüsünden **Çıkış**'ı seçin. Kaldırma ve güncelleme `%LOCALAPPDATA%\WinDesk` içindeki düzeni ve saklanan kısayolları silmez.

## Kaynak kodu

Bu depo yayınları dağıtır. Tam C# / WPF projesi, testler ve derleme betikleri [WinDesk-0.7.6-source.zip](https://github.com/LogicalTR/WinDesk/releases/download/v0.7.6/WinDesk-0.7.6-source.zip) dosyasındadır. GitHub'ın otomatik **Source code** arşivleri yalnız bu dağıtım deposunu içerir; tam proje için ekli kaynak arşivini indirin.

0.7.6 ile Windows 10 desteği ve Unicode kısayol düzeltmeleri geldi. Windows 10 ve 11'de 55 çekirdek ve 114 WPF/masaüstü kontrolü geçti. Windows 10 VM'de .NET kurulmadan kurulum/açılış/kaldırma ve 8 gerçek Explorer yeniden başlatma kontrolü de doğrulandı.
