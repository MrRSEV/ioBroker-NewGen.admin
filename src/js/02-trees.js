const treeData={};
function expandKeys(v){if(Array.isArray(v))return v.map(expandKeys);if(v&&typeof v==='object'){const out={};for(const [k,val] of Object.entries(v)){const e=expandKeys(val);const parts=k.split('.').filter(p=>p!=='');if(parts.length<2){out[k]=e;continue;}let cur=out,ok=true;for(let i=0;i<parts.length-1;i++){const p=parts[i];if(!(p in cur))cur[p]={};else if(cur[p]===null||typeof cur[p]!=='object'||Array.isArray(cur[p])){ok=false;break;}cur=cur[p];}const last=parts[parts.length-1];if(ok&&!(last in cur))cur[last]=e;else out[k]=e;}return out;}return v;}
function itemLabel(x,i){if(x&&typeof x==='object'){for(const n of ['name','username','id','Name','Id'])if(typeof x[n]==='string'&&x[n])return x[n];}return String(i);}
function renderTree(boxId,data){treeData[boxId]=data;const box=$(boxId+'Table');
if(data&&!Array.isArray(data)&&typeof data==='object'&&typeof data.error==='string'){box.textContent=data.error;return;}
if(data==null||(typeof data==='object'&&Object.keys(data).length===0)){                    box.textContent=t('ui.noData','Keine Daten');return;}
const table=document.createElement('table');table.className='tree';const head=table.createTHead().insertRow();for(const title of [t('table.key','Schlüssel'),t('table.value','Wert')]){const th=document.createElement('th');th.textContent=title;head.appendChild(th);}
const body=table.createTBody();
function entriesOf(v){return Array.isArray(v)?v.map((x,i)=>[itemLabel(x,i),x]):Object.entries(v);}
function addRows(value,label,depth,key){const container=value!==null&&typeof value==='object';const entries=container?entriesOf(value):[];const expandable=entries.length>0;const stateKey=boxId+':'+key;const open=expandable&&(treeToggled.has(stateKey)?depth>=1:depth<1);
const row=body.insertRow();const cell=row.insertCell();cell.className='tree-id';cell.style.paddingLeft=(0.6+depth*1.2)+'rem';const arrow=document.createElement('span');arrow.className='arrow';arrow.textContent=expandable?(open?'▼':'▶'):'';cell.appendChild(arrow);const text=document.createElement('span');text.textContent=label;if(container)text.className='folder';cell.appendChild(text);
if(expandable){row.style.cursor='pointer';row.onclick=()=>{if(treeToggled.has(stateKey))treeToggled.delete(stateKey);else treeToggled.add(stateKey);renderTree(boxId,treeData[boxId]);};}
const valueCell=row.insertCell();if(container){valueCell.className='muted';valueCell.textContent=Array.isArray(value)?'['+value.length+']':'{'+entries.length+'}';}else{valueCell.textContent=value===null?'null':String(value);}
if(open)for(const [k,x] of entries)addRows(x,k,depth+1,key+'/'+k);}
const top=expandKeys(data);if(top!==null&&typeof top==='object'){for(const [k,x] of entriesOf(top))addRows(x,k,0,k);}else{addRows(top,'Wert',0,'value');}
box.replaceChildren(table);}
const treeToggled=new Set();
const objOpen=new Set();
let objSel='';
let objInitDone=false;
function objSelectFromHash(){const m=/^#tab-objects\/select\/(.+)$/.exec(ngInitialHash);if(!m)return;objSel=decodeURIComponent(m[1]);const parts=objSel.split('.');for(let i=1;i<=parts.length;i++)objOpen.add(parts.slice(0,i).join('.'));}
function objAll(open,list){objOpen.clear();if(open)for(const o of list)for(const p of expandPaths(o.Id))objOpen.add(p);}
function expandPaths(id){const parts=String(id).split('.');const out=[];for(let i=1;i<parts.length;i++)out.push(parts.slice(0,i).join('.'));return out;}
let objList=[];
async function writeState(id,value){try{await api('/api/states/write',{id,value});}catch(e){$('status').textContent=' '+e.message;}}
function renderObjects(list){const box=$('statesTable');if(box.contains(document.activeElement)&&document.activeElement.classList.contains('oinput'))return;if(list&&!Array.isArray(list)&&typeof list.error==='string'){box.textContent=list.error;return;}if(!Array.isArray(list)||!list.length){box.textContent=t('ui.noData','Keine Daten');return;}
if(!objInitDone){objInitDone=true;objSelectFromHash();}objList=list;
const scroller=box.closest('.body')||box;const scrollTop=scroller.scrollTop;
const root={name:'',path:'',children:new Map(),item:null};
for(const o of list){let node=root,path='';for(const part of String(o.Id).split('.')){path=path?path+'.'+part:part;if(!node.children.has(part))node.children.set(part,{name:part,path,children:new Map(),item:null});node=node.children.get(part);}node.item=o;}
const table=document.createElement('table');table.className='objtree';const head=table.createTHead().insertRow();for(const title of [t('table.id','ID'),t('table.name','Name'),t('table.type','Typ'),t('table.role','Rolle'),t('table.room','Raum'),t('table.function','Funktion'),t('table.value','Wert'),'']){const th=document.createElement('th');th.textContent=title;head.appendChild(th);}
const body=table.createTBody();
const fmtVal=v=>v===null||v===undefined?'':typeof v==='object'?JSON.stringify(v):String(v);
function addNode(node,depth){const kids=[...node.children.values()].sort((x,y)=>x.name.localeCompare(y.name,undefined,{numeric:true}));const expandable=kids.length>0;const open=expandable&&objOpen.has(node.path);const item=node.item;const isState=item&&item.Type==='state';const meta=(item&&item.Meta)||{};
const row=body.insertRow();if(objSel===node.path)row.className='sel';
row.onclick=()=>{objSel=node.path;if(expandable){if(objOpen.has(node.path))objOpen.delete(node.path);else objOpen.add(node.path);}renderObjects(objList);};
const c0=row.insertCell();c0.className='oid';c0.style.paddingLeft=(0.6+depth*1.6)+'rem';c0.append(el('span',{className:'oico',textContent:isState?'\uD83D\uDCC4':open?'\uD83D\uDCC2':'\uD83D\uDCC1'}),el('span',{textContent:node.name}));
const c1=row.insertCell();c1.className='oname';c1.title=item?item.Name:'';c1.append(el('span',{className:'oico',textContent:item?(item.Type==='adapter'||item.Type==='meta'?'\u2699':item.Type==='channel'?'\u24D8':(item.State&&item.State.Value!==null&&item.Meta&&item.Meta.read==='true'&&/connected/.test(node.name)?'\uD83D\uDCF6':'')):''}),document.createTextNode(item&&item.Name&&item.Name!==node.name?item.Name:''));
const c2=row.insertCell();if(item){c2.append(el('span',{className:'otype'},el('i',{className:'obox',textContent:isState?'\u25A1':''}),document.createTextNode(item.Type)));}
const c3=row.insertCell();c3.textContent=item&&item.StateDefinition&&item.StateDefinition.Role?item.StateDefinition.Role:(meta.role||'');
const c4=row.insertCell();c4.textContent=meta.room||'';
const c5=row.insertCell();c5.textContent=meta.function||'';
const c6=row.insertCell();c6.className='oval';
if(isState){const v=item.State?item.State.Value:null;const writable=meta.write!=='false';const type=meta.type||typeof v;
if(type==='boolean'||typeof v==='boolean'){const sw=el('input',{type:'checkbox',checked:v===true,disabled:!writable||!perms.has('StateWrite'),className:'oswitch'});sw.onclick=e=>e.stopPropagation();sw.onchange=()=>writeState(item.Id,sw.checked);c6.append(sw);}
else if(writable&&perms.has('StateWrite')){const isNum=type==='number'||(type!=='string'&&typeof v==='number');const inp=el('input',{type:isNum?'number':'text',step:'any',value:fmtVal(v),className:'oinput',readOnly:true,title:'Doppelklick zum Bearbeiten'});inp.onclick=e=>e.stopPropagation();inp.ondblclick=e=>{e.stopPropagation();inp.readOnly=false;inp.focus();inp.select();};inp.onblur=()=>{inp.readOnly=true;inp.value=fmtVal(v);};c6.append(inp);
const commit=()=>{const s=inp.value;if(s===fmtVal(v))return;let out=s;if(isNum){if(s.trim()===''||isNaN(Number(s)))return;out=Number(s);}writeState(item.Id,out);};
inp.onkeydown=e=>{e.stopPropagation();if(inp.readOnly)return;if(e.key==='Enter'){commit();inp.blur();}else if(e.key==='Escape'){inp.value=fmtVal(v);inp.blur();}};if(meta.unit)c6.append(el('span',{className:'ounit',textContent:' '+meta.unit}));}
else{const val=fmtVal(v)+(v!==null&&v!==undefined&&meta.unit?' '+meta.unit:'');c6.textContent=val;c6.title=val;}}
const c7=row.insertCell();c7.className='ogear';c7.textContent=isState?'\u2699':'';
if(open)for(const kid of kids)addNode(kid,depth+1);}
for(const kid of [...root.children.values()].sort((x,y)=>x.name.localeCompare(y.name,undefined,{numeric:true})))addNode(kid,0);
box.replaceChildren(table);scroller.scrollTop=scrollTop;}
$('objExpand').onclick=()=>{objAll(true,objList);renderObjects(objList);};
$('objCollapse').onclick=()=>{objAll(false,objList);renderObjects(objList);};
function fmtDate(v){if(!v)return '';const d=new Date(v);return isNaN(d)?String(v):d.toLocaleString('de-DE');}
