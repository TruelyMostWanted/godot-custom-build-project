# 278-feature-data-file-text-editor

Data files in the script editor: CSV, XML, TOML and YAML highlighters and live schema errors. The feature is editor-only, so the
automatic checks cover the runtime parts (schema file patterns, line numbers, issue cap, JSON line resolving, the project setting
if the editor registered it). The rest is checked by hand.

## Manual checks in the editor

1. Open Project > Project Settings > Filesystem > Data > Schema Files and add `res://278-feature-data-file-text-editor/data/items_schema.tres`.
2. Open `data/items_bad.csv` in the script editor. Expect the invalid lines (5, 6, 7, 8) marked, and a status line like
   `N issues. Line L: ...`. Clicking the status cycles through the issues. The multi-line cell (lines 3-4) must not shift the marks.
3. Open `data/items_ok.csv`. Expect no marks.
4. Open `data/sample.xml`, `data/sample.toml`, `data/sample.yaml` and `data/items_ok.csv`. Expect syntax highlighting for each.
5. In a build with `module_toml_enabled=no` or `module_yaml_enabled=no` the TOML or YAML highlighter is absent (plain text).
6. Editor settings: Text Editor > Data Files (live parse and schema validation size limits) exist and take effect.
