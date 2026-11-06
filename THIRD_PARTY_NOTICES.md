# Third-Party Notices

This package includes or depends on the following third-party software.

---

## VOSK Speech Recognition Toolkit

- **Project:** https://github.com/alphacep/vosk-api
- **Author:** Alpha Cephei Inc. (https://alphacephei.com/)
- **License:** Apache License 2.0
- **Usage:** This package bundles prebuilt VOSK native libraries for offline speech recognition. `libvosk.so` for Android arm64 ships under `Runtime/Plugins/Android/arm64-v8a/`. `libvosk.dll` for Windows x86_64 -- together with its three MinGW runtime dependencies (`libgcc_s_seh-1.dll`, `libstdc++-6.dll`, `libwinpthread-1.dll`) -- ships under `Runtime/Plugins/x86_64/` for the Editor live-microphone backend. Sources for both are available at the [VOSK releases](https://github.com/alphacep/vosk-api/releases) page.
- **Models:** VOSK language models (e.g. `vosk-model-small-en-us-0.15`) are downloaded separately by the developer and are not included in this package. Models are provided by Alpha Cephei under the Apache 2.0 license.

### Apache License 2.0

```
Copyright 2019-2024 Alpha Cephei Inc.

Licensed under the Apache License, Version 2.0 (the "License");
you may not use this file except in compliance with the License.
You may obtain a copy of the License at

    http://www.apache.org/licenses/LICENSE-2.0

Unless required by applicable law or agreed to in writing, software
distributed under the License is distributed on an "AS IS" BASIS,
WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
See the License for the specific language governing permissions and
limitations under the License.
```

---

## Kaldi

- **Project:** https://github.com/kaldi-asr/kaldi
- **Author:** Kaldi contributors
- **License:** Apache License 2.0
- **Usage:** VOSK is built on top of the Kaldi speech recognition toolkit. Kaldi components are compiled into the VOSK native libraries.

---

## OpenFST

- **Project:** https://www.openfst.org/
- **Author:** Google Inc. and contributors
- **License:** Apache License 2.0
- **Usage:** Used by Kaldi/VOSK for finite-state transducer operations in the speech decoder.

---

## WebRTC (webrtc-audio-processing)

- **Project:** https://webrtc.org/ (source taken from the webrtc-audio-processing 2.1 release, https://freedesktop.org/software/pulseaudio/webrtc-audio-processing/)
- **Author:** The WebRTC project authors
- **License:** BSD 3-Clause License, with the WebRTC Additional IP Rights Grant (Patents)
- **Usage:** The AGC2 automatic gain controller and the resampler, voice-activity and utility code it depends on are compiled from source into `libvosk-bridge.so` under `Runtime/Plugins/Android/arm64-v8a/`, where they form the bridge's audio front end.

### BSD 3-Clause License

```
Copyright (c) 2011, The WebRTC project authors. All rights reserved.

Redistribution and use in source and binary forms, with or without
modification, are permitted provided that the following conditions are
met:

  * Redistributions of source code must retain the above copyright
    notice, this list of conditions and the following disclaimer.

  * Redistributions in binary form must reproduce the above copyright
    notice, this list of conditions and the following disclaimer in
    the documentation and/or other materials provided with the
    distribution.

  * Neither the name of Google nor the names of its contributors may
    be used to endorse or promote products derived from this software
    without specific prior written permission.

THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS
"AS IS" AND ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT
LIMITED TO, THE IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS FOR
A PARTICULAR PURPOSE ARE DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT
HOLDER OR CONTRIBUTORS BE LIABLE FOR ANY DIRECT, INDIRECT, INCIDENTAL,
SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES (INCLUDING, BUT NOT
LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES; LOSS OF USE,
DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND ON ANY
THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
(INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE
OF THIS SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
```

### Additional IP Rights Grant (Patents)

```
Additional IP Rights Grant (Patents)

"This implementation" means the copyrightable works distributed by
Google as part of the WebRTC code package.

Google hereby grants to you a perpetual, worldwide, non-exclusive,
no-charge, irrevocable (except as stated in this section) patent
license to make, have made, use, offer to sell, sell, import,
transfer, and otherwise run, modify and propagate the contents of this
implementation of the WebRTC code package, where such license applies
only to those patent claims, both currently owned by Google and
acquired in the future, licensable by Google that are necessarily
infringed by this implementation of the WebRTC code package. This
grant does not include claims that would be infringed only as a
consequence of further modification of this implementation. If you or
your agent or exclusive licensee institute or order or agree to the
institution of patent litigation against any entity (including a
cross-claim or counterclaim in a lawsuit) alleging that this
implementation of the WebRTC code package or any code incorporated
within this implementation of the WebRTC code package constitutes
direct or contributory patent infringement, or inducement of patent
infringement, then any patent rights granted to you under this License
for this implementation of the WebRTC code package shall terminate as
of the date such litigation is filed.
```

---

## RNNoise (RNN VAD weights)

- **Project:** https://github.com/xiph/rnnoise
- **Author:** Mozilla, Jean-Marc Valin, Xiph.Org Foundation, Mark Borgerding
- **License:** BSD 3-Clause License
- **Usage:** The RNN voice-activity-detector weights WebRTC carries under `third_party/rnnoise` are compiled into `libvosk-bridge.so` as part of the audio front end.

### BSD 3-Clause License

```
Copyright (c) 2017, Mozilla
Copyright (c) 2007-2017, Jean-Marc Valin
Copyright (c) 2005-2017, Xiph.Org Foundation
Copyright (c) 2003-2004, Mark Borgerding

Redistribution and use in source and binary forms, with or without
modification, are permitted provided that the following conditions
are met:

- Redistributions of source code must retain the above copyright
notice, this list of conditions and the following disclaimer.

- Redistributions in binary form must reproduce the above copyright
notice, this list of conditions and the following disclaimer in the
documentation and/or other materials provided with the distribution.

- Neither the name of the Xiph.Org Foundation nor the names of its
contributors may be used to endorse or promote products derived from
this software without specific prior written permission.

THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS
``AS IS'' AND ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT
LIMITED TO, THE IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS FOR
A PARTICULAR PURPOSE ARE DISCLAIMED.  IN NO EVENT SHALL THE FOUNDATION
OR CONTRIBUTORS BE LIABLE FOR ANY DIRECT, INDIRECT, INCIDENTAL,
SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES (INCLUDING, BUT NOT
LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES; LOSS OF USE,
DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND ON ANY
THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
(INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE
OF THIS SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
```

---

## PFFFT

- **Project:** https://bitbucket.org/jpommier/pffft
- **Author:** Julien Pommier, based on FFTPACKv4 by Paul Swarztrauber (NCAR)
- **License:** FFTPACKv5 license (BSD-style)
- **Usage:** The PFFFT fast Fourier transform WebRTC carries under `third_party/pffft` is compiled into `libvosk-bridge.so` as part of the audio front end.

### FFTPACKv5 License

```
Copyright (c) 2013  Julien Pommier ( pommier@modartt.com )

Based on original fortran 77 code from FFTPACKv4 from NETLIB,
authored by Dr Paul Swarztrauber of NCAR, in 1985.

As confirmed by the NCAR fftpack software curators, the following
FFTPACKv5 license applies to FFTPACKv4 sources. My changes are
released under the same terms.

FFTPACK license:

http://www.cisl.ucar.edu/css/software/fftpack5/ftpk.html

Copyright (c) 2004 the University Corporation for Atmospheric
Research ("UCAR"). All rights reserved. Developed by NCAR's
Computational and Information Systems Laboratory, UCAR,
www.cisl.ucar.edu.

Redistribution and use of the Software in source and binary forms,
with or without modification, is permitted provided that the
following conditions are met:

- Neither the names of NCAR's Computational and Information Systems
Laboratory, the University Corporation for Atmospheric Research,
nor the names of its sponsors or contributors may be used to
endorse or promote products derived from this Software without
specific prior written permission.

- Redistributions of source code must retain the above copyright
notices, this list of conditions, and the disclaimer below.

- Redistributions in binary form must reproduce the above copyright
notice, this list of conditions, and the disclaimer below in the
documentation and/or other materials provided with the
distribution.

THIS SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND,
EXPRESS OR IMPLIED, INCLUDING, BUT NOT LIMITED TO THE WARRANTIES OF
MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND
NONINFRINGEMENT. IN NO EVENT SHALL THE CONTRIBUTORS OR COPYRIGHT
HOLDERS BE LIABLE FOR ANY CLAIM, INDIRECT, INCIDENTAL, SPECIAL,
EXEMPLARY, OR CONSEQUENTIAL DAMAGES OR OTHER LIABILITY, WHETHER IN AN
ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN
CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS WITH THE
SOFTWARE.
```

---

## Abseil

- **Project:** https://github.com/abseil/abseil-cpp
- **Author:** The Abseil Authors
- **License:** Apache License 2.0
- **Usage:** abseil-cpp 20240722.0 (`absl::strings` and the abseil libraries it depends on), required by the WebRTC code, is compiled into `libvosk-bridge.so` as part of the audio front end. The Apache License 2.0 notice is the one reproduced under VOSK above; abseil ships no NOTICE file.
