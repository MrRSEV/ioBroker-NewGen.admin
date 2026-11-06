const perms=new Set();
function fmtDur(s){s=Math.max(0,Math.floor(Number(s)||0));const d=Math.floor(s/86400),h=Math.floor(s%86400/3600),m=Math.floor(s%3600/60),x=s%60;return (d?d+'d ':'')+(d||h?h+'h ':'')+(d||h||m?m+'m ':'')+x+'s';}
async function adapterAction(id,action,button){button.disabled=true;try{await api('/api/adapters/'+action,{adapterId:id});}catch(error){alert(error.message);}refresh();}
function cfgLabel(v,d){if(v==null)return d||'';if(typeof v==='string')return t(v,v);const l=(curLang||'en').slice(0,2);return v[curLang]||v[l]||v.en||Object.values(v)[0]||d||'';}
function cfgCond(src,data){if(src===true||src===false)return src;if(typeof src!=='string'||!src.trim())return false;
const tok=src.match(/'[^']*'|\x22[^\x22]*\x22|===|!==|==|!=|<=|>=|&&|\|\||[()!<>]|[\w.]+/g)||[];let i=0;
const get=p=>{const parts=p.split('.');if(parts[0]!=='data'&&parts[0]!=='globalData')return undefined;let v=data;for(const k of parts.slice(1)){if(v==null)return undefined;v=v[k];}return v;};
const prim=()=>{const x=tok[i++];if(x==='('){const v=or();i++;return v;}if(x==='!')return !prim();if(x===undefined)return undefined;if(/^['\x22]/.test(x))return x.slice(1,-1);if(x==='true')return true;if(x==='false')return false;if(x==='null')return null;if(x==='undefined')return undefined;if(!isNaN(Number(x)))return Number(x);return get(x);};
const cmp=()=>{let a=prim();while(['===','!==','==','!=','<','>','<=','>='].includes(tok[i])){const o=tok[i++];const b=prim();a=o==='==='?a===b:o==='!=='?a!==b:o==='=='?a==b:o==='!='?a!=b:o==='<'?a<b:o==='>'?a>b:o==='<='?a<=b:a>=b;}return a;};
const and=()=>{let a=cmp();while(tok[i]==='&&'){i++;const b=cmp();a=a&&b;}return a;};
const or=()=>{let a=and();while(tok[i]==='||'){i++;const b=and();a=a||b;}return a;};
try{return !!or();}catch{return false;}}
function cfgItems(node){const it=node.items;if(!it)return[];if(Array.isArray(it))return it.map(x=>[x.attr,x]);return Object.entries(it);}
function cfgBuild(parent,node,key,data,ctx){const type=node.type||'text';const attr=node.attr||key;const box=el('div',{className:'cfgitem'});parent.append(box);
const label=cfgLabel(node.label,attr);const help=cfgLabel(node.help,'');
const refresh2=()=>{box.classList.toggle('hidden',!!node.hidden&&cfgCond(node.hidden,data));const dis=!!node.disabled&&cfgCond(node.disabled,data);box.querySelectorAll('input,select,textarea').forEach(x=>x.disabled=dis);};ctx.updates.push(refresh2);
const done=()=>{if(help&&type!=='panel'&&type!=='tabs')box.append(el('small',{className:'muted',textContent:help}));ctx.updates.forEach(f=>f());};
if(type==='panel'){if(label&&node.label)box.append(el('h3',{textContent:label}));const grid=el('div',{className:'cfggrid'});box.append(grid);for(const [k,child] of cfgItems(node))cfgBuild(grid,child,k,data,ctx);return;}
if(type==='tabs'){const bar=el('div',{className:'cfgtabs'});const body=el('div');box.append(bar,body);const tabs=cfgItems(node);const panes=tabs.map(([k,child])=>{const pane=el('div',{className:'hidden'});cfgBuild(pane,{...child,type:'panel',label:undefined},k,data,ctx);body.append(pane);const b=el('button',{type:'button',textContent:cfgLabel(child.label,k)});bar.append(b);return[b,pane];});
const sel=n=>panes.forEach(([b,p],j)=>{b.classList.toggle('on',j===n);p.classList.toggle('hidden',j!==n);});sel(0);panes.forEach(([b],j)=>b.onclick=()=>sel(j));return;}
if(type==='header'||type==='staticText'){box.append(el(type==='header'?'h3':'p',{textContent:cfgLabel(node.text||node.label,'')}));return;}
if(type==='divider'){box.append(el('hr'));return;}
if(type==='checkbox'){const cb=el('input',{type:'checkbox',checked:!!data[attr]});cb.onchange=()=>{data[attr]=cb.checked;ctx.updates.forEach(f=>f());};box.append(el('label',{className:'chk'},cb,document.createTextNode(' '+label)));done();return;}
let input;
if(type==='select'){input=el('select');for(const o of node.options||[])input.append(el('option',{value:String(o.value),textContent:cfgLabel(o.label,String(o.value))}));input.value=String(data[attr]??'');input.onchange=()=>{const o=(node.options||[]).find(x=>String(x.value)===input.value);data[attr]=o?o.value:input.value;ctx.updates.forEach(f=>f());};}
else if(type==='textarea'){input=el('textarea',{rows:node.minRows||4,value:data[attr]==null?'':String(data[attr])});input.oninput=()=>{data[attr]=input.value;ctx.updates.forEach(f=>f());};}
else{const num=type==='number';input=el('input',{type:num?'number':type==='password'?'password':'text',value:data[attr]==null?'':String(data[attr])});if(num){if(node.min!=null)input.min=node.min;if(node.max!=null)input.max=node.max;}if(type==='password')input.autocomplete='new-password';input.oninput=()=>{data[attr]=num?(input.value===''?'':Number(input.value)):input.value;ctx.updates.forEach(f=>f());};}
field(box,label,input);done();}
function cfgFallback(values){const items={};for(const [k,v] of Object.entries(values))items[k]={type:typeof v==='boolean'?'checkbox':typeof v==='number'?'number':'text',label:k};return{type:'panel',items};}
