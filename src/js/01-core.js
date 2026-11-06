const $=id=>document.getElementById(id);let timer=null;
function nav(name){document.querySelectorAll('.panel').forEach(p=>p.classList.toggle('hidden',p.dataset.panel!==name));document.querySelectorAll('.drawer .item').forEach(i=>i.classList.toggle('sel',i.dataset.panel===name));localStorage.setItem('ngPanel',name);history.replaceState(null,'','#tab-'+name);}
document.querySelectorAll('.drawer .item').forEach(i=>i.onclick=()=>nav(i.dataset.panel));
nav(location.hash.startsWith('#tab-')&&document.querySelector('.drawer .item[data-panel="'+location.hash.slice(5)+'"]')?location.hash.slice(5):document.querySelector('.drawer .item[data-panel="'+localStorage.getItem('ngPanel')+'"]')?localStorage.getItem('ngPanel'):'overview');
function setCompact(on){document.body.classList.toggle('compact',on);localStorage.setItem('ngCompact',on?'1':'');}
setCompact(!!localStorage.getItem('ngCompact'));
$('menuBtn').onclick=()=>setCompact(!document.body.classList.contains('compact'));
function setDark(on){document.documentElement.classList.toggle('dark',on);localStorage.setItem('ngTheme',on?'dark':'light');}
setDark((localStorage.getItem('ngTheme')||(matchMedia('(prefers-color-scheme: dark)').matches?'dark':'light'))==='dark');
$('themeBtn').onclick=()=>setDark(!document.documentElement.classList.contains('dark'));
let i18n={};const t=(k,d)=>i18n[k]||d||k;
function applyI18n(){document.querySelectorAll('[data-i18n]').forEach(e=>{e.textContent=t(e.dataset.i18n,e.textContent);});document.querySelectorAll('.tabs button').forEach(b=>{b.textContent=t('view.'+b.dataset.view,b.textContent);});document.documentElement.lang=(i18n['language.code']||curLang||'de').slice(0,2);}
let curLang=localStorage.getItem('ngLang')||'';
async function loadI18n(lang){try{const r=await fetch('/api/i18n/translations',{method:'POST',headers:{'Content-Type':'application/json'},body:JSON.stringify(lang?{language:lang}:{})});const d=await r.json();i18n=d.translations||{};curLang=d.language||curLang;}catch(e){}applyI18n();if(typeof refresh==='function'&&!$('app').classList.contains('hidden'))refresh();}
async function initLanguages(){try{const r=await fetch('/api/i18n/languages');const d=await r.json();if(!curLang)curLang=d.language;document.querySelectorAll('.langsel').forEach(s=>{s.replaceChildren(...d.languages.map(l=>{const o=document.createElement('option');o.value=l.code;o.textContent=l.name+' ('+l.code+')';return o;}));s.value=curLang;s.onchange=()=>{curLang=s.value;localStorage.setItem('ngLang',curLang);document.querySelectorAll('.langsel').forEach(x=>x.value=curLang);loadI18n(curLang);};});}catch(e){}await loadI18n(curLang);}
initLanguages();
function show(name){for(const id of ['login','change','setup','app','firstrun'])$(id).classList.toggle('hidden',id!==name);if(name!=='app'&&timer){clearInterval(timer);timer=null;}}
document.querySelectorAll('.tabs').forEach(tabs=>tabs.querySelectorAll('button').forEach(button=>{button.onclick=()=>{const id=tabs.dataset.for;$(id+'Table').classList.toggle('hidden',button.dataset.view!=='table');$(id).classList.toggle('hidden',button.dataset.view!=='json');tabs.querySelectorAll('button').forEach(x=>x.classList.toggle('active',x===button));};}));
