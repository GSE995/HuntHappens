# Camper — тело основного игрока

Модель пользователя из Meshy_AI_cartoon_camper_rigged_biped подключена к существующему FPS Player в Assets/Scenes/ForestPrototype.unity и Assets/Prefabs/PrototypePlayer.prefab.

## Просмотр
Открой ForestPrototype и нажми Play. WASD и мышь работают как раньше; в FPS видны текущие игровые руки и тень тела. Полная модель видна в Scene и других камерах. Для осмотра найди FPS Player / Camper Player Body в Hierarchy и нажми F в Scene. Дополнительная игровая камера не создана.

## Реализация
- Models/Camper.fbx — исходный персонаж со скелетом. Models/Walking.fbx — исходная ходьба. Оригинальные FBX не изменены.
- Textures — BaseColor, Metallic, Roughness и созданная MetallicSmoothness, где alpha = 1 - roughness. URP/Lit; карты metallic/roughness импортируются в линейном пространстве, основной цвет в sRGB, разрешение импорта ограничено 2048. Исходные PNG сохранены целиком.
- Camper.controller — Generic, общий Avatar для модели и ходьбы, BlendTree Idle/Walk по Speed. Camper_Idle.anim — расслабленная неподвижная поза, подготовленная на исходном скелете.
- CamperPlayerVisual.prefab — только визуальная модель без Collider/Rigidbody. Root motion выключен. Движение, прыжки, камера, физика и взаимодействия остаются у исходного FPS-контроллера.
- PlayerCharacterVisual считывает фактическую горизонтальную скорость CharacterController. Скорость referenceWalkSpeed на компоненте задаёт переход к полной ходьбе; это настройка представления, не изменение скорости игрока.
- В FPS полная модель рисуется только в тенях. В других камерах отображается целиком. Голова и руки модели не перекрывают FPS-камеру; исходные игровые FPS-руки сохраняются и скрываются от внешних камер. Состояние отображения восстанавливается после рендера. Новые слои и глобальные настройки URP не добавлены. Для вида собственных ног нужна отдельно подготовленная FPS-сетка; экспериментальная обрезка исходной одежды не включена.


## Проверка
PlayerCharacterChecks.RunBatch проверяет Avatar, единственную исходную камеру, отсутствие лишней физики, сохранность игровых рук и камеры, переход Idle → Walk → Idle, движение кости стопы, отсутствие root-motion drift и восстановление видимости. Снимки находятся в Logs/Camper-Idle.png, Camper-Walking.png, Camper-FPS-LookDown.png и Camper-FPS-Forward.png.

При закрытом редакторе этого проекта:

```powershell
& 'C:/Program Files/Unity/Hub/Editor/6000.6.6f1/Editor/Unity.exe' -batchmode -projectPath 'E:/unity/HuntHappens' -executeMethod HuntHappens.Editor.PlayerCharacterChecks.RunBatch -logFile 'E:/unity/HuntHappens/Logs/CamperPlay.log'
```

Не добавляй -quit: тест завершает редактор сам. Успех — CAMPER_PLAY_CHECKS_PASSED. Для повторной генерации есть Tools / Hunt Happens / Configure Camper Player Body; команда перегенерирует визуальные ассеты и их подключения, поэтому ручные изменения модели/контроллера сначала сохрани в отдельных ассетах.

## Ограничения
В исходнике есть только ходьба: отдельных клипов бега, прыжка, стрейфа и движения назад нет. При прыжке используется Idle; при движении вбок и назад пока проигрывается та же ходьба вперёд. Контакт стоп с землёй и IK не добавлялись, поэтому возможна небольшая пробуксовка при текущей скорости FPS-игрока. Сетка Meshy и её веса сохранены, стилизация деталей зависит от исходных текстур.

Масштаб тела подгоняется по вершинам модели в первом кадре Idle до роста 1,80 м; CharacterController также 1,80 м, камера на высоте 1,68 м от ног. `Editor/PlayerCharacterSetup.cs` согласует размеры через `WorldScaleSetup.ConfigurePlayer`. Подробности и проверка — `Assets/WorldScale-README.md`.
