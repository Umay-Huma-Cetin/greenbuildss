; EcoBuildss kurulum dosyası (Inno Setup 6.3 ve üstü)
; Önce kurulum\yayinla.cmd çalıştırılır; o, programı kurulum\cikti klasörüne yayınlar ve bu betiği derler.

#define Surum "3.0.0"

[Setup]
AppId={{5F0D6B9E-3C1A-4E8F-9A7B-EC0B1D550001}
AppName=EcoBuildss
AppVersion={#Surum}
AppPublisher=Umay Hüma Çetin
AppCopyright=Umay Hüma Çetin
DefaultDirName={autopf}\EcoBuildss
DefaultGroupName=EcoBuildss
DisableProgramGroupPage=yes
OutputDir=kurulum_cikti
OutputBaseFilename=EcoBuildss-Kurulum-{#Surum}
Compression=lzma2
SolidCompression=yes
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
PrivilegesRequired=admin
WizardStyle=modern
UninstallDisplayIcon={app}\EcoBuildss.exe
; Veritabanı ve yedekler C:\ProgramData\EcoBuildss (ya da yöneticinin seçtiği ağ klasörü) içindedir; kaldırınca silinmez (firma verisi kaybolmasın).

[Languages]
Name: "turkish"; MessagesFile: "compiler:Languages\Turkish.isl"

[Tasks]
Name: "masaustu"; Description: "Masaüstüne kısayol koy"; GroupDescription: "Kısayollar:"

[Dirs]
; Veritabanının ortak klasörü: bilgisayardaki bütün Windows kullanıcıları aynı veriyi kullanır
Name: "{commonappdata}\EcoBuildss"; Permissions: users-modify

[Files]
Source: "cikti\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\EcoBuildss"; Filename: "{app}\EcoBuildss.exe"
Name: "{group}\EcoBuildss'i kaldır"; Filename: "{uninstallexe}"
Name: "{autodesktop}\EcoBuildss"; Filename: "{app}\EcoBuildss.exe"; Tasks: masaustu

[Run]
Filename: "{app}\EcoBuildss.exe"; Description: "EcoBuildss'i başlat"; Flags: nowait postinstall skipifsilent
