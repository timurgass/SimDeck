'use strict';
let fs25RenderKey = '', fs25Expanded = false;
const fs25StateLabels={"fs25Lower": ["Поднято", "Опущено"], "fs25LowerAll": ["Все подняты", "Все опущены"], "fs25TurnOn": ["Выключено", "Работает"], "fs25TurnOnAll": ["Все выключены", "Все работают"], "fs25Motor": ["Остановлен", "Запущен"], "fs25Fold": ["Сложено", "Разложено"], "fs25Lights": ["Выключен", "Включён"], "fs25HighBeam": ["Выключен", "Включён"], "fs25WorkLightFront": ["Выключен", "Включён"], "fs25WorkLightBack": ["Выключен", "Включён"], "fs25Beacon": ["Выключен", "Включён"], "fs25TurnLeft": ["Выключен", "Включён"], "fs25TurnRight": ["Выключен", "Включён"], "fs25Hazard": ["Выключена", "Включена"], "fs25Cruise": ["Выключен", "Включён"], "fs25Cover": ["Закрыто", "Открыто"], "fs25Pipe": ["Убрана", "Выдвинута"], "fs25Chopper": ["Валок", "Измельчение"], "fs25Helper": ["Не работает", "Работает"], "fs25Pause": ["Время идёт", "Время остановлено"], "fs25Radio": ["Выключено", "Включено"], "fs25DoubleSpray": ["Выключена", "Включена"], "fs25Axle": ["Опущена", "Поднята"]};
function renderFs25(profileId, frame) {
  const active = profileId === 'fs25', card = document.getElementById('fs25Overview');
  document.getElementById('deck').classList.toggle('fs25Deck', active);
  document.querySelector('.gauges').hidden = active;
  card.hidden = !active;
  if (!active) { fs25RenderKey = ''; return; }
  const details = frame?.data?.fs25, report = frame?.data?.fs25Advisor;
  const liveFresh = Number.isFinite(frame?.ageMs) && frame.ageMs + performance.now() - frameAt < 1500;
  const key = String(frame?.sequence) + '|' + fs25Expanded + '|' + Boolean(details) + '|' + liveFresh;
  if (fs25RenderKey === key) return;
  fs25RenderKey = key;
  card.replaceChildren();
  const add = (tag, className, value) => {
    const element = document.createElement(tag);
    if (className) element.className = className;
    element.textContent = value;
    card.append(element);
    return element;
  };
  add('small', 'eyebrow', 'ХОЗЯЙСТВО · ДАННЫЕ СОХРАНЕНИЯ');
  const live = liveFresh ? frame.data?.actionStates : null;
  const status = [['fs25Lower','Орудие', 'поднято', 'опущено'],['fs25TurnOn','Агрегат','выключен','работает'],['fs25Motor','Двигатель','остановлен','запущен']]
    .filter(([id]) => typeof live?.[id] === 'boolean').map(([id,name,off,on]) => name + ': ' + (live[id] ? on : off));
  add('p', status.length ? 'fs25Row' : 'hint', status.length ? 'Сейчас · ' + status.join(' · ') : 'Текущее состояние орудия: нет данных от мода FS25_SimDeckStatus');
  if (!details) {
    add('p', 'hint', 'Сохранение FS25 пока не найдено. Сохраните игру и проверьте путь в Companion.');
    return;
  }
  add('h2', '', details.header?.savegameName || 'Без названия');
  add('p', 'hint', [details.header?.mapTitle, details.period?.russianMonth].filter(Boolean).join(' · '));
  const saved = details.timestamp ? new Date(details.timestamp).toLocaleString('ru-RU') : '—';
  add('p', details.isStale ? 'warning' : 'hint', 'Сохранено: ' + saved + (details.isStale ? ' · данные устарели' : ''));
  const farm = details.playerFarm || details.farms?.[0];
  if (farm) add('p', '', 'Деньги: ' + Number(farm.money || 0).toLocaleString('ru-RU') + ' € · кредит ' + Number(farm.loan || 0).toLocaleString('ru-RU') + ' €');
  const fields = fs25FieldRows(details);
  add('h3', 'settingTitle', 'Поля · ' + fields.length);
  for (const f of fs25Expanded ? fields : fields.slice(0, 6))
    add('p', 'fs25Row', `№${f.id} · ${fs25CropName(f.fruitType)} · ${f.groundType || 'состояние неизвестно'} · сорняки ${fs25FieldPercent(f.weedState,9)??'—'}${fs25FieldPercent(f.weedState,9)===null?'':'%'} · известь ${fs25FieldPercent(f.limeLevel,3)??'—'}${fs25FieldPercent(f.limeLevel,3)===null?'':'%'} · удобрение ${fs25FieldPercent(f.sprayLevel,3)??'—'}${fs25FieldPercent(f.sprayLevel,3)===null?'':'%'}`);
  if (fields.length > 6) {
    const button = add('button', 'wide', fs25Expanded ? 'Свернуть поля' : 'Показать все поля');
    button.onclick = () => { fs25Expanded = !fs25Expanded; renderFs25(profileId, frame); };
  }
  if (report?.alerts?.length) {
    add('h3', 'settingTitle', 'Советник · ' + report.alerts.length + ' уведомлений');
    for (const alert of report.alerts.slice(0, 6)) add('p', alert.severity >= 2 ? 'warning' : 'fs25Row', alert.message);
  }
  if (report?.planName) {
    add('h3', 'settingTitle', 'План: ' + report.planName);
    for (const task of (report.tasks || []).slice(0, 6))
      add('p', 'fs25Row', (['К исполнению', '✓', 'Вне сезона', 'Нет поля'][task.status] || 'К исполнению') + ' · ' + (task.task?.title || ''));
  }
}
