# Vendored dictionary data (feature 023, task T006)

Data files used by the deterministic layer of the lyric writing assistant (research R-02, R-04,
R-10). They are copied **byte-identical** from the upstream sources below (CRLF line endings of
OpenTaal and SCOWL included; `.gitattributes` in this folder disables line-ending normalisation).
Nothing is downloaded at run time. `manifest.json` lists every file with its SHA-256 and is
checked by `tests/StemStudio.Api.Tests/LyricAssist/VendoredDictionaryTests.cs`.

Retrieved 2026-11-17. Every pinned release or commit is older than 14 days.

## Dutch: OpenTaal Hunspell `nl` 2.20.23

- Upstream: https://github.com/OpenTaal/opentaal-hunspell
- Pinned commit: `1c22bc3d61b2735afee5f27a3499b7026cfcf661` (2022-11-29, commit message
  "2.20.23"; `datetimeversion.txt` = `2022-11-29 17:48:41 2.20.23`). The later commit
  `ad0351b7` (2023-03-10, "intermediate") is an untagged work-in-progress state and is not used.
- Raw files: `https://raw.githubusercontent.com/OpenTaal/opentaal-hunspell/1c22bc3d61b2735afee5f27a3499b7026cfcf661/<file>`
- Licence: dual "Revised BSD License (3-clause) and/or CC BY 3.0" (`LICENSE.txt` items 3 and 6;
  also the header of `nl.aff`). **StemStudio uses BSD-3-Clause.** `LICENSE.txt` and
  `datetimeversion.txt` (referenced by the licence for date and version) are kept with the data.

## English: SCOWL Hunspell `en_US`, `en_GB-ise`, `en_GB-ize` 2026.02.25

- Upstream: https://github.com/en-wl/wordlist (SCOWL, https://wordlist.aspell.net)
- Release `rel-2026.02.25` (published 2026-02-25), SCOWL commit `7e99eda`; assets
  `hunspell-en_US-2026.02.25.zip` (SHA-256 `ac8e73310e951d88c52c2cf2ba54ceaca34f8486a81630ac8a75dc5f931179f9`),
  `hunspell-en_GB-ise-2026.02.25.zip` (`d6fbb91ae7824c52fb02f74d7bc2cd9092f130faec60f42326a59437fa7247a3`),
  `hunspell-en_GB-ize-2026.02.25.zip` (`c5bdd92fc1e21da7503939fe4139f36f28f01c18ac35e2fc90b526a1b7bfb099`)
  from `https://github.com/en-wl/wordlist/releases/download/rel-2026.02.25/`.
- These are the SCOWL-generated dictionaries (`speller/make-hunspell-dict`, SCOWL size 60), not
  the LibreOffice en_GB dictionary.
- Licence (text in each `README_<dict>.txt`, section "COPYRIGHT, SOURCES, and CREDITS"): word
  lists under the SCOWL permissive notice ("Permission to use, copy, modify, distribute, and
  sell ... provided that the above copyright notice appears in all copies", Kevin Atkinson,
  MIT/HPND-style); the affix files under Geoff Kuenning's Ispell BSD-style licence (advertising
  clause removed). Recorded as licence `SCOWL` in `manifest.json`. The READMEs are the licence
  files and must stay with the data.

## English pronunciation: CMU Pronouncing Dictionary

- Upstream: https://github.com/cmusphinx/cmudict
- Pinned commit: `74790861f652b15e4ac49015a90074ad62a27690` (2025-10-24, merge of PR #44); the
  dictionary content is that of the repository head at retrieval time.
- Raw files: `https://raw.githubusercontent.com/cmusphinx/cmudict/74790861f652b15e4ac49015a90074ad62a27690/<file>`
- `cmudict.dict`: 135,166 lines, ARPAbet phones with stress digits, LF line endings.
- Licence: `LICENSE` = BSD-2-Clause wording (Carnegie Mellon University, 1993-2015; no
  advertising clause). Recorded as `BSD-2-Clause`.

## Files

| Path | Bytes | SHA-256 | Licence |
|---|---|---|---|
| `nl/LICENSE.txt` | 25,817 | `fd9e95c360245eab3c388885062820754785faa4123964994cfdecb950b71948` | BSD-3-Clause (licence file) |
| `nl/datetimeversion.txt` | 28 | `733c469a79928e1cdcff9fd7e0d86e94ac8a991435521aa8536c48e2286586d0` | BSD-3-Clause |
| `nl/nl.aff` | 46,575 | `b4d6263eb73c46c47759b0f605e324e48d157bdbd57a8f6bf2b2d051549d9966` | BSD-3-Clause |
| `nl/nl.dic` | 2,491,328 | `f1bcc9f6c3c71709d7e29463f1c45c50c26c7c3b172808545e3596bac8a75331` | BSD-3-Clause |
| `en_US/README_en_US.txt` | 6,976 | `d63c711523fc9504fb71e1278489c6d7bb8cafcba75158c107cd17844955470f` | SCOWL (licence file) |
| `en_US/en_US.aff` | 3,240 | `526e1dc32d1c0e8806dd72809c017b5546f2104b08b9424a4c47311aca526cca` | SCOWL (Kuenning BSD-style affix) |
| `en_US/en_US.dic` | 508,614 | `ab012ae86ce4b46b60db5b150869288de9c4a87bc747911764e582125b1b961e` | SCOWL |
| `en_GB/README_en_GB-ise.txt` | 6,984 | `72827bd23b623a253f7928657d68a7b020eea3c7ab8bdfbb1e61323bc8b010c4` | SCOWL (licence file) |
| `en_GB/README_en_GB-ize.txt` | 6,984 | `2f489193f265faee0f6903611034b62be5cd69142bcf8166899069f196236940` | SCOWL (licence file) |
| `en_GB/en_GB-ise.aff` | 3,244 | `e5478627cc7413a2874134b518c9a3dc667a8886414c72ed47eea65185bd3a88` | SCOWL (Kuenning BSD-style affix) |
| `en_GB/en_GB-ise.dic` | 508,689 | `4fdb00d235ac2f0634f455f757ba15036261f1557e1b693ac264ee42dbd7c7eb` | SCOWL |
| `en_GB/en_GB-ize.aff` | 3,244 | `4e202ea23a829f8f4abb0c7912b929bcdc1665b549779be4d4c2272e7b265fef` | SCOWL (Kuenning BSD-style affix) |
| `en_GB/en_GB-ize.dic` | 508,622 | `70288072b3f6d95ac757c4199af1539fb52af841b5135725eae783316c646943` | SCOWL |
| `cmudict/LICENSE` | 1,754 | `bd4ce8e44170a5f9f481310ca85c51de3c4f851a65e679b40e603b143bd3542a` | BSD-2-Clause (licence file) |
| `cmudict/cmudict.dict` | 3,618,488 | `81917843c7f44ce2b094ac63873c2c7a4cf802040792c455ba3ca406891c3d22` | BSD-2-Clause |

Total 7,740,587 bytes (about 7.4 MiB) plus this file, `manifest.json` and `.gitattributes`.

## Updating

Download the new upstream release into an empty scratch folder, copy the same file set, update
`manifest.json` (path, `sha256`, `bytes`, `licence`) and this file, re-read each licence file, and
run the vendored-dictionary tests. Releases must be at least 14 days old.
