# Edi.CacheAside.InMemory

[![.NET Build and Pack](https://github.com/EdiWang/Edi.CacheAside.InMemory/actions/workflows/dotnet.yml/badge.svg)](https://github.com/EdiWang/Edi.CacheAside.InMemory/actions/workflows/dotnet.yml)

In memory cache helpers for ASP.NET Core for implement cache aside pattern in small and non-distributed apps

> If you are looking for cloud native app design pattern, this is not for you. Please use a distributed cache product like Redis to implement Cache Aside pattern on your cloud app.

## Install from NuGet

```powershell
dotnet add package Edi.CacheAside.InMemory
```

```powershell
NuGet\Install-Package Edi.CacheAside.InMemory
```

```xml
<PackageReference Include="Edi.CacheAside.InMemory" Version="1.0.0" />
```

## Usage

### ASP.NET Core

```csharp
services.AddInMemoryCacheAside();
```

TODO

```csharp
```

## 免责申明

此项目(Edi.CacheAside.InMemory)及其配套组件均为免费开源的产品，仅用于学习交流，并且不直接向中国提供服务，中国用户请于下载后立即删除。

任何中国境内的组织及个人不得使用此项目(Edi.CacheAside.InMemory)及其配套组件构建任何形式的面向中国境内用户的网站或服务。

不可用于任何违反中华人民共和国(含台湾省)或使用者所在地区法律法规的用途。

因为作者即本人仅完成代码的开发和开源活动(开源即任何人都可以下载使用)，从未参与用户的任何运营和盈利活动。

且不知晓用户后续将程序源代码用于何种用途，故用户使用过程中所带来的任何法律责任即由用户自己承担。

[《开源软件有漏洞，作者需要负责吗？是的！》](https://go.edi.wang/aka/os251)