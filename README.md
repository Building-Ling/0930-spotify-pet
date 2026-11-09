# 0930 Spotify Pet

Windows 桌宠。它随 Spotify 的播放状态切换睡觉和唱歌动画，并提供播放、暂停、切歌和进度拖动。右键角色可以切换置顶或关闭应用。

## 本地开发

在 Windows 上安装 .NET 10 SDK 和 Spotify 桌面版。克隆仓库后，在仓库根目录运行：

```powershell
dotnet run --project SpotifyPet/SpotifyPet.csproj
```

登录 Spotify 并播放一首歌，即可测试桌宠与播放器的联动。

## 构建安装包

在 Windows 上用 .NET 10 SDK 发布，然后用 Inno Setup 6.7 或更新版本生成安装包。将下面的 `ISCC.exe` 替换成你本机编译器的路径。

```powershell
dotnet publish SpotifyPet/SpotifyPet.csproj -c Release -r win-x64 --self-contained true -o dist/win-x64
& 'ISCC.exe' "/DPayloadDir=$PWD\dist\win-x64" "/O$PWD\dist" SpotifyPet/Installer.iss
```

生成的安装包没有代码签名。启用了 Smart App Control 的 Windows 设备可能拦截桌宠启动。

## 声明

本项目为个人学习和非商业用途制作，与 Spotify 及角色权利人无关联。角色形象的相关权利归其权利人所有。仓库公开不授予角色素材的再使用权。
