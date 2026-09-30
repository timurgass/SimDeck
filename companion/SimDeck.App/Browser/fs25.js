'use strict';
let fs25RenderKey = '', fs25Expanded = false;
function renderFs25(profileId, frame) {
  const active = profileId === 'fs25', card = document.getElementById('fs25Overview');
  document.getElementById('deck').classList.toggle('fs25Deck', active);
  document.querySelector('.gauges').hidden = active;
  card.hidden = !active;
  if (!active) { fs25RenderKey = ''; return; }
  const details = frame?.data?.fs25, report = frame?.data?.fs25Advisor;
  const key = String(frame?.sequence) + '|' + fs25Expanded + '|' + Boolean(details);
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
  const fields = details.fields || [];
  add('h3', 'settingTitle', 'Поля · ' + fields.length);
  for (const f of fs25Expanded ? fields : fields.slice(0, 6))
    add('p', 'fs25Row', `№${f.id} · ${f.fruitType || 'пусто'} · ${f.groundType || 'состояние неизвестно'} · сорняки ${f.weedState}/9 · известь ${f.limeLevel}/3`);
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
