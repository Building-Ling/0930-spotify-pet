#ifndef PayloadDir
  #error Pass the published app directory as /DPayloadDir=<path>.
#endif

[Setup]
AppId=0930SpotifyPet
AppName=0930 Spotify Pet
AppVersion=0.1.0
DefaultDirName={localappdata}\Programs\0930 Spotify Pet
DefaultGroupName=0930 Spotify Pet
DisableProgramGroupPage=yes
PrivilegesRequired=lowest
ArchitecturesAllowed=x64compatible
MinVersion=10.0.19041
OutputBaseFilename=0930-Spotify-Pet-Setup
Compression=lzma2
SolidCompression=yes

[Files]
Source: "{#PayloadDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{userprograms}\0930 Spotify Pet"; Filename: "{app}\SpotifyPet.exe"; WorkingDir: "{app}"
