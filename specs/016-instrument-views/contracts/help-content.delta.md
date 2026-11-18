# Contract delta: help content 1.1.0 (feature 016, additive)

Base: `specs/004-design-system-ux/contracts/help-content.md` **1.0.0** (unchanged since 004; 012-015 put their
topics in existing areas). 1.1.0 adds the area **`instruments`** (folder `src/StemStudio.Web/Help/topics/instruments/`)
and nothing else; every 1.0.0 rule (front matter, levels, anchors, `reviewed`, tests) applies. Adding an area is a minor
change (help-content.md "Versioning").

- Schema: [`help-topic.schema.json`](help-topic.schema.json) = the 004 schema with `instruments` appended to `area.enum`;
  linked into `tests/StemStudio.Web.Tests` instead of the 004 file (016 Phase 0, T004).
- Area order in the help index: after `mixes`, before `glossary` (frontend: `TopicParser.AreaOrder`, task T071).
- Topics, anchors and glossary additions: [DELTA.md](DELTA.md) section 5.
