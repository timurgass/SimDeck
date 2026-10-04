'use strict';
// Compact, original line icons. The same action-to-symbol mapping is used on Android.
const fs25Symbols = Object.freeze({
  back: "M20 8 L6 24 L20 40 M6 24 H42",
  implement: 'M6 33 H42 M11 32 L16 19 H32 L37 32 M18 19 V13 H30 V19 M24 13 V7 M17 38 H31',
  lower: 'M8 12 H40 M15 12 V29 H33 V12 M24 20 V40 M17 33 L24 40 L31 33',
  power: 'M24 5 V25 M15 10 C4 20 12 40 24 41 C36 40 44 20 33 10',
  fold: 'M6 35 L19 22 L29 22 L42 35 M6 13 L19 26 M42 13 L29 26 M20 17 H28 M20 31 H28',
  width: 'M7 22 H41 M7 22 L14 15 M7 22 L14 29 M41 22 L34 15 M41 22 L34 29 M19 12 V32 H29 V12',
  mode: 'M8 14 H40 M8 24 H40 M8 34 H40 M17 10 V18 M30 20 V28 M23 30 V38',
  seed: 'M9 12 H39 L34 25 H14 Z M17 25 V31 M24 25 V34 M31 25 V31 M15 39 C12 35 18 33 19 39 M29 39 C26 35 32 33 33 39',
  crop: 'M24 41 V18 M24 27 C17 27 13 22 13 16 C19 16 24 19 24 27 M24 23 C31 23 36 17 36 11 C28 11 24 15 24 23 M17 41 H31',
  spray: 'M9 13 H28 V32 H9 Z M14 13 V8 H23 V13 M28 19 H35 L40 15 M35 23 L42 23 M35 27 L40 31 M15 38 H31',
  pipe: 'M6 35 V23 H21 V17 H37 V10 H43 M17 35 V23 M31 17 V29 M27 35 H35',
  unload: 'M8 10 H35 V28 H8 Z M35 19 H43 M24 25 V40 M17 33 L24 40 L31 33',
  cover: 'M7 24 H41 L37 36 H11 Z M7 20 C14 10 34 10 41 20 M13 12 L17 7 M35 12 L31 7',
  chopper: 'M24 7 V41 M7 24 H41 M12 12 L36 36 M36 12 L12 36 M24 17 C31 17 31 31 24 31 C17 31 17 17 24 17 Z',
  direction: 'M6 16 H37 M30 9 L37 16 L30 23 M42 32 H11 M18 25 L11 32 L18 39',
  gear: 'M12 9 V37 M24 9 V37 M36 9 V37 M12 23 H36 M9 9 H15 M21 9 H27 M33 9 H39 M9 37 H15 M21 37 H27 M33 37 H39',
  helper: 'M24 8 C28 8 31 11 31 15 C31 19 28 22 24 22 C20 22 17 19 17 15 C17 11 20 8 24 8 Z M11 40 C11 31 16 27 24 27 C32 27 37 31 37 40 M5 23 L12 23 M36 23 L43 23',
  clock: 'M24 6 C14 6 6 14 6 24 C6 34 14 42 24 42 C34 42 42 34 42 24 C42 14 34 6 24 6 Z M24 13 V24 L32 29',
  radio: 'M7 19 H41 V37 H7 Z M12 19 L33 10 M15 27 H26 M15 32 H26 M34 25 C30 25 30 33 34 33 C38 33 38 25 34 25 Z',
  store: 'M7 16 H41 L38 38 H10 Z M11 16 L15 8 H33 L37 16 M18 24 V38 M30 24 V38 M18 24 H30',
});
const fs25IconKinds = Object.freeze({
  fs25Lower:'lower',fs25LowerAll:'lower',fs25TurnOn:'power',fs25TurnOnAll:'power',
  fs25Fold:'fold',fs25WorkWidth:'width',fs25WorkMode:'mode',
  fs25Attach:'hitch',fs25NextImplement:'implement',fs25PrevImplement:'implement',
  fs25Extra2:'implement',fs25Extra3:'implement',fs25Extra4:'implement',
  fs25Seeds:'crop',fs25SeedsBack:'crop',fs25DoubleSpray:'spray',
  fs25Pipe:'pipe',fs25Unload:'unload',fs25UnloadHere:'unload',fs25TipSide:'unload',fs25Cover:'cover',fs25Chopper:'chopper',
  fs25Motor:'engine',fs25Direction:'direction',fs25Cruise:'cruise',
  fs25GearUp:'gear',fs25GearDown:'gear',fs25GroupUp:'gear',fs25GroupDown:'gear',
  fs25Lights:'lights',fs25HighBeam:'high',fs25WorkLightFront:'lights',fs25WorkLightBack:'lights',fs25Beacon:'beacon',
  fs25TurnLeft:'left',fs25TurnRight:'right',fs25Hazard:'hazard',fs25Horn:'horn',fs25Camera:'camera',fs25Axle:'axle',
  fs25Helper:'helper',fs25NextVehicle:'direction',fs25PrevVehicle:'direction',fs25Enter:'helper',fs25Seat:'helper',
  fs25Menu:'mode',fs25Back:'back',fs25Store:'store',fs25Map:'map',fs25Construction:'store',fs25Help:'mode',
  fs25Pause:'pause',fs25TimeUp:'clock',fs25TimeDown:'clock',fs25Radio:'radio'
});
const fs25IconPaths = Object.freeze(Object.fromEntries(Object.entries(fs25IconKinds).map(([id,kind]) => [id,
  fs25Symbols[kind] || ets2IconPaths[{
    hitch:'etsAttachTrailer', engine:'etsEngine', cruise:'etsCruise', lights:'etsLights',
    high:'etsHighBeam', beacon:'etsBeacon', left:'etsLeftSignal', right:'etsRightSignal',
    hazard:'etsHazards', horn:'etsHorn', camera:'etsCamera', axle:'etsLiftAxle',
    map:'etsMap', pause:'etsPause'
  }[kind]]])));
