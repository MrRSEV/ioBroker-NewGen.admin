async function loadJson(path){try{return await api(path);}catch(error){if(error.message==='Nicht angemeldet')throw error;return {error:error.message};}}
async function refresh(){try{const [states,adapters,config,roles,users]=await Promise.all([loadJson('/api/states'),loadJson('/api/adapters'),loadJson('/api/config'),loadJson('/api/roles'),loadJson('/api/users')]);$('states').textContent=JSON.stringify(states,null,2);renderObjects(states);$('adapters').textContent=JSON.stringify(adapters,null,2);renderAdapters(adapters);refreshOverview(adapters);renderInstances(adapters);loadLegacy();$('config').textContent=JSON.stringify(config,null,2);renderTree('config',config);$('roles').textContent=JSON.stringify(roles,null,2);renderTree('roles',roles);$('users').textContent=JSON.stringify(users,null,2);renderTree('users',users);lastUsers=users;lastRoles=roles;renderManage();$('status').textContent=' Verbunden';}catch(error){$('status').textContent=' '+error.message;}}
const setupOptions={temperatureUnit:['°C','°F'],dateFormat:['DD.MM.YYYY','DD/MM/YYYY','MM/DD/YYYY','YYYY-MM-DD'],isFloatComma:[[true,'Komma - 3,14'],[false,'Punkt - 3.14']],firstDayOfWeek:[['monday','Montag'],['sunday','Sonntag'],['saturday','Samstag']]};
function setupSelect(form,label,key,opts,val){const s=el('select');for(const o of opts){const [v,l]=Array.isArray(o)?o:[o,o];s.append(el('option',{value:String(v),textContent:l}));}s.value=String(val);return field(form,label,s);}
function showSetup(cfg){const form=$('setupForm');form.replaceChildren();const g=k=>cfg['system.'+k];const err=el('div',{className:'error'});
const unit=setupSelect(form,t('setup.temperature','Temperatureinheit'),'temperatureUnit',setupOptions.temperatureUnit,g('temperatureUnit'));
const cur=field(form,t('setup.currency','Währung'),el('input',{type:'text',maxLength:10,value:g('currency')||''}));
const date=setupSelect(form,t('setup.dateFormat','Datumsformat'),'dateFormat',setupOptions.dateFormat,g('dateFormat'));
const comma=setupSelect(form,t('setup.separator','Trennzeichen in Zahlen'),'isFloatComma',setupOptions.isFloatComma,g('isFloatComma'));
const week=setupSelect(form,t('setup.weekStart','Die Woche beginnt mit'),'firstDayOfWeek',setupOptions.firstDayOfWeek,g('firstDayOfWeek'));
const country=field(form,t('setup.country','Land'),el('input',{type:'text',maxLength:100,value:g('country')||''}));
const city=field(form,t('setup.city','Stadt'),el('input',{type:'text',maxLength:100,value:g('city')||''}));
const addr=field(form,t('setup.address','Adresse'),el('input',{type:'text',maxLength:200,value:g('address')||''}));
form.append(el('small',{className:'muted',textContent:t('setup.addressHint','Wird nur zur Positionsberechnung verwendet.')}));
const lat=field(form,t('setup.latitude','Breitengrad'),el('input',{type:'text',inputMode:'decimal',value:g('latitude')||''}));
const lon=field(form,t('setup.longitude','Längengrad'),el('input',{type:'text',inputMode:'decimal',value:g('longitude')||''}));
const loc=el('button',{type:'button',className:'btn2',textContent:t('setup.locate','Position des Browsers verwenden')});
loc.onclick=()=>{if(!navigator.geolocation){err.textContent='Geolocation nicht verfügbar';return;}navigator.geolocation.getCurrentPosition(p=>{lat.value=p.coords.latitude.toFixed(6);lon.value=p.coords.longitude.toFixed(6);},e=>{err.textContent=e.message;});};
const skip=el('button',{type:'button',className:'btn2',textContent:t('setup.skip','Überspringen')});
const ok=el('button',{type:'submit',textContent:t('setup.save','Speichern und weiter')});
form.append(loc,err,el('div',{className:'dlgbtn'},skip,ok));
const finish=async extra=>{err.textContent='';ok.disabled=skip.disabled=true;try{await api('/api/config',{...extra,'system.setupDone':true});await start();}catch(x){err.textContent=x.message;ok.disabled=skip.disabled=false;}};
skip.onclick=()=>finish({});
form.onsubmit=e=>{e.preventDefault();finish({'system.temperatureUnit':unit.value,'system.currency':cur.value,'system.dateFormat':date.value,'system.isFloatComma':comma.value==='true','system.firstDayOfWeek':week.value,'system.country':country.value,'system.city':city.value,'system.address':addr.value,'system.latitude':lat.value,'system.longitude':lon.value});};
show('setup');}
