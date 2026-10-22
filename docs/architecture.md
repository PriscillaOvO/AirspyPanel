# Architecture and extension points

The application remains WPF on .NET Framework 4.8. There is no runtime plugin loader. New backends are explicitly constructed and injected so a future decoder or map adapter can be added without giving plugins arbitrary execution rights.

| Boundary | Responsibility |
| --- | --- |
| `IReceiverBackend` | Own exactly the decoder process started by the panel; expose output, exit, running state and CPU, and start/stop operations. `Decoder` implements it. |
| `IMapService` | Probe a map API, return structured receiver/configuration state, configure VRS, and manage normal start/close. `VrsService` implements it. |
| Beast monitor/parser | Decode framing and aircraft data independently from the WPF view. |
| Settings store | Read/migrate/write per-user XML atomically. Stable language, theme and sampling IDs are persisted rather than translated labels. |
| Discovery | Bounded, cancelable directory search; candidates and verification remain distinct from saving settings. |
| Localization | Six embedded XML catalogs and stable keys. UI renders text at presentation boundaries. |
| Event buffer | Bounded events with time, stable severity, resource-key parts and raw parameters; translated at display/export time. Raw decoder output is separate. |
| Presentation/lifecycle | Navigation, responsive layout, tray, theme and user-facing decisions; never part of the Beast parser. |

`PanelApp` is the composition/presentation coordinator. Its constructor accepts receiver and map interfaces. Preview tests can supply substitutes; normal startup creates the Airspy decoder and VRS adapter. The coordinator still owns control binding and workflow sequencing; it is not a full MVVM framework or a claim that every future change needs no UI work.

VRS API availability, source match and local configuration match are separate fields. Concurrent requests share a task; configuration revision changes invalidate outdated responses. Background checks retain the last visible state while pending. Stop/exit and VRS setup are serialized, and closing a VRS process times out without a force kill. Stop targets local VRS processes, including manually started VRS; the decoder is limited to the panel-owned process.

## Localization contract

Catalogs are in `src/Resources`: `zh-CN`, `en-US`, `ja-JP`, `fr-FR`, `de-DE`, `ru-RU`. Each contains the same stable `<text key="…">` set. `L.Get(key)` renders the selected language; unsupported system locales resolve to English. `L.Source(key)` emits an internal resource token rather than translated text. `L.Capture` decomposes composed messages into stable key parts and opaque parameters for deferred log rendering. Parameter text is never searched for Chinese phrases or translated by substring. New messages should prefer a complete resource entry rather than creating more fragmented phrases. These tokens are internal only, resolved before display and export; they are not a public wire format.

Do not infer state from translated strings. Log levels, themes, sampling, page IDs and VRS failure kinds remain stable identifiers. Resource keys are append-only; `sNNN` keys are legacy keys, new keys should describe intent. External error details, user paths, receiver names and raw output are not translation targets. Language changes must preserve process handles, input values, page, mode, buffers and statistics.

New settings default to `Language=System`, while XML without `Language` migrates to Chinese to preserve the old interface. Legacy sampling `自动` migrates to `auto`. Both portable and installed editions share the same per-user settings; there is no second hidden portable configuration.
