# Third-party software notices

Audora is licensed under the PolyForm Noncommercial License 1.0.0 (see [LICENSE](LICENSE)).
The software below was made by other people. It is included in the Audora downloads under its own licence.

| Component | What Audora uses it for | Licence | Copyright holders |
|---|---|---|---|
| [NAudio](https://github.com/naudio/NAudio) 2.2.1 | Reading and playing audio on Windows | MIT | Mark Heath |
| [.NET 8 runtime](https://github.com/dotnet/runtime), including Windows Desktop (WPF and Windows Forms) | Runs the app. It is included, so you do not need to install .NET | MIT | .NET Foundation and Contributors |
| System.ServiceProcess.ServiceController 8.0.1 | Checking that the Windows audio services are running | MIT | .NET Foundation and Contributors |

The .NET runtime also contains smaller parts from other projects. Their notices are listed in the
[.NET third-party notices](https://github.com/dotnet/runtime/blob/main/THIRD-PARTY-NOTICES.TXT).

## Not included

- **Inno Setup** builds the installer. It is a build tool and is not part of Audora.
- **VB-CABLE** is an optional virtual audio cable that the Full control mode can use. It is made by VB-Audio,
  is not included and is not installed by Audora. You get it from its own website under its own licence.

## MIT License

This text applies to NAudio, the .NET runtime and ServiceController above.

    MIT License

    Copyright 2020 Mark Heath (NAudio)
    Copyright (c) .NET Foundation and Contributors (.NET and ServiceController)

    Permission is hereby granted, free of charge, to any person obtaining a copy
    of this software and associated documentation files (the "Software"), to deal
    in the Software without restriction, including without limitation the rights
    to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
    copies of the Software, and to permit persons to whom the Software is
    furnished to do so, subject to the following conditions:

    The above copyright notice and this permission notice shall be included in all
    copies or substantial portions of the Software.

    THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
    IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
    FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
    AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
    LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
    OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
    SOFTWARE.
