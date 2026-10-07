# Third-party notices

The library uses these independently published MIT-licensed NuGet packages.
Their source is not copied into this repository or embedded in the package:

- Yoga.Net 3.2.3 — package source revision `1c7fd4a1ede445f5a3f9d5ae9239364b09d25154`,
  [source](https://github.com/chenrensong/Yoga.Net), MIT.
- Wcwidth 4.0.1 — package source revision `64a3f523d5dc27124e04ff9079137ac2c5c72143`,
  [source](https://github.com/spectreconsole/wcwidth), MIT.

No source-wrapper packages are published by this project. Package lock files
record the restored versions and content hashes. The historical research
documents describe earlier source snapshots, not the current dependencies.

Selected literal regression inputs and expectations in `tests/Blazor.Ink.Checks` are
translated from Ink's `truncate-width.tsx`, `clip-wide-background.tsx`,
`overlap-wide-background.tsx`, and `styled-combining-marks.tsx` at
`26d2c3f83008142061c22267482489588cc3823c`. Their original license follows.
The complete upstream differential corpus has not been executed or ported.

Legacy input framing/key mappings and selected executable packet fixtures are
translated from Ink's `src/input-parser.ts`, `src/parse-keypress.ts`,
`src/hooks/use-input.ts`, `test/input-parser.ts`, and `test/parse-keypress.ts`
at the same pinned revision. Ink's parser credits Enquirer keypress code at
`36785f3399a41cd61e9d28d1eb9c2fcd73d69b4c` (MIT).

## Enquirer

Copyright (c) 2016-present, Jon Schlinkert.

MIT License — permission is granted under the same MIT terms reproduced below.

## Ink

MIT License

Copyright (c) Vadym Demedes <vadimdemedes@hey.com> (https://github.com/vadimdemedes)
Copyright (c) Sindre Sorhus <sindresorhus@gmail.com> (https://sindresorhus.com)

Permission is hereby granted, free of charge, to any person obtaining a copy of this software and associated documentation files (the "Software"), to deal in the Software without restriction, including without limitation the rights to use, copy, modify, merge, publish, distribute, sublicense, and/or sell copies of the Software, and to permit persons to whom the Software is furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
