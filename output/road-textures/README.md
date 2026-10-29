# Эскиз грунтовых дорожек

`dirt-path-atlas-v1.png` — первый вариант атласа из пяти участков: прямой, поворот, Т-перекрёсток, пересечение, S-изгиб. Создан встроенным image_gen по общему стилю пользовательских референсов; не подключён к игровой сцене. Это эскиз: перед использованием требуется проверить альфа-канал и стыковку, убрать ореолы и разделить участки.

Промпт: top-down modular dirt path atlas, five isolated pieces (straight, 90-degree turn, T junction, cross junction, S bend), warm sandy tan, chunky angular low-poly color patches, subtle soil streaks, uniform connection widths, transparent background, no perspective, cast shadows, labels, grass or stones.

## Вариант 2
`dirt-path-atlas-v2.png` создан встроенным image_gen с четырьмя исходными скриншотами как референсами. Формы и угловатый рисунок ближе к образцам. В результате всё ещё виден ореол; это эскиз, не готовая текстура для сцены.
Промпт: faithful cleaned recreation of the four supplied road screenshots, 2x2 atlas, cross junction, curved corner, T junction, S bend; chunky flat-shaded polygon facets, muted beige/tan, broad directional streaks; orthographic overhead; transparent padding, no glow, shadows, text or neighboring objects.
