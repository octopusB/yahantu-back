const canvas=document.querySelector('#chart'),ctx=canvas.getContext('2d'),select=document.querySelector('#refrigerant');
const colors={measured:'#47d7c8',standard:'#ffb454',liquid:'#55a9ff',vapor:'#e8759d'};
let lastData=null;
async function init(){
  const names=await fetch('/api/refrigerants').then(check).then(r=>r.json());
  select.innerHTML=names.map(x=>`<option>${escapeHtml(x)}</option>`).join('');
  select.onchange=async()=>{await fetch('/api/refrigerant',{method:'POST',headers:{'content-type':'application/json'},body:JSON.stringify({refrigerant:select.value})}).then(check);await load()};
  await load();setInterval(load,1000);addEventListener('resize',()=>lastData&&draw(lastData));
}
async function load(){
  const data=await fetch('/api/diagram').then(check).then(r=>r.json());lastData=data;select.value=data.refrigerant;
  document.querySelector('#backend').textContent=data.propertyBackend;
  document.querySelector('#updated').textContent=data.updatedAt.startsWith('0001')?'尚未采样':new Date(data.updatedAt).toLocaleTimeString();
  const error=document.querySelector('#error');error.hidden=!data.error;error.textContent=data.error||'';
  document.querySelector('#empty').style.display=data.measuredPoints.length?'none':'grid';
  document.querySelector('#conditions').textContent=`标准工况：蒸发 ${data.conditions.evaporatingTemperatureC} °C / 冷凝 ${data.conditions.condensingTemperatureC} °C / 过热 ${data.conditions.superheatK} K / 过冷 ${data.conditions.subcoolingK} K`;
  renderTable(data);draw(data);
}
function renderTable(data){
  const rows=(items,kind)=>items.map((p,i)=>`<tr><td><i class="dot ${kind}"></i>${i+1}</td><td>${escapeHtml(p.pointName)}</td><td>${p.temperatureC.toFixed(2)}</td><td>${p.pressureMpa.toFixed(4)}</td><td>${p.logPressure.toFixed(4)}</td><td>${p.enthalpyKjKg.toFixed(2)}</td></tr>`).join('');
  document.querySelector('#values').innerHTML=rows(data.measuredPoints,'measured')+rows(data.standardCycle,'standard');
}
function draw(data){
  const series=[...data.saturatedLiquid,...data.saturatedVapor,...data.standardCycle,...data.measuredPoints];if(!series.length)return;
  const dpr=devicePixelRatio||1,w=canvas.clientWidth,h=canvas.clientHeight;canvas.width=w*dpr;canvas.height=h*dpr;ctx.setTransform(dpr,0,0,dpr,0,0);ctx.clearRect(0,0,w,h);
  const hs=series.map(p=>p.enthalpyKjKg),logs=series.map(p=>p.logPressure),pad=(Math.max(...hs)-Math.min(...hs))*.08;
  const minH=Math.floor((Math.min(...hs)-pad)/20)*20,maxH=Math.ceil((Math.max(...hs)+pad)/20)*20,minL=Math.floor((Math.min(...logs)-.12)*10)/10,maxL=Math.ceil((Math.max(...logs)+.12)*10)/10;
  const m={l:76,r:32,t:28,b:62},pw=w-m.l-m.r,ph=h-m.t-m.b,x=v=>m.l+(v-minH)/(maxH-minH)*pw,y=v=>m.t+(maxL-v)/(maxL-minL)*ph;
  ctx.font='12px sans-serif';ctx.lineWidth=1;ctx.strokeStyle='#1d3544';ctx.fillStyle='#78909c';
  for(let v=minH;v<=maxH+.1;v+=20){ctx.beginPath();ctx.moveTo(x(v),m.t);ctx.lineTo(x(v),m.t+ph);ctx.stroke();ctx.fillText(v.toFixed(0),x(v)-10,h-28)}
  for(let v=minL;v<=maxL+.001;v+=.1){ctx.beginPath();ctx.moveTo(m.l,y(v));ctx.lineTo(m.l+pw,y(v));ctx.stroke();ctx.fillText(v.toFixed(1),34,y(v)+4)}
  ctx.fillStyle='#9ab0bc';ctx.fillText('比焓 h (kJ/kg)',w/2-36,h-7);ctx.save();ctx.translate(14,h/2+32);ctx.rotate(-Math.PI/2);ctx.fillText('lg P（P: MPa 绝对压力）',0,0);ctx.restore();
  line(data.saturatedLiquid,colors.liquid,1.7,false,x,y);line(data.saturatedVapor,colors.vapor,1.7,false,x,y);line(data.standardCycle,colors.standard,2,true,x,y);line(data.measuredPoints,colors.measured,2.8,true,x,y);
  points(data.standardCycle,colors.standard,4,x,y,'S');points(data.measuredPoints,colors.measured,6,x,y,'M');
  ctx.fillStyle=colors.liquid;ctx.fillText('饱和液线',m.l+8,m.t+16);ctx.fillStyle=colors.vapor;ctx.fillText('饱和蒸汽线',m.l+80,m.t+16);ctx.fillStyle=colors.standard;ctx.fillText('标准循环',m.l+174,m.t+16);ctx.fillStyle=colors.measured;ctx.fillText('实测循环',m.l+244,m.t+16);
}
function line(items,color,width,closed,x,y){if(!items.length)return;ctx.strokeStyle=color;ctx.lineWidth=width;ctx.setLineDash(color===colors.standard?[7,5]:[]);ctx.beginPath();items.forEach((p,i)=>i?ctx.lineTo(x(p.enthalpyKjKg),y(p.logPressure)):ctx.moveTo(x(p.enthalpyKjKg),y(p.logPressure)));if(closed)ctx.closePath();ctx.stroke();ctx.setLineDash([])}
function points(items,color,radius,x,y,prefix){items.forEach((p,i)=>{ctx.fillStyle=color;ctx.beginPath();ctx.arc(x(p.enthalpyKjKg),y(p.logPressure),radius,0,Math.PI*2);ctx.fill();ctx.fillStyle='#eaf3f7';ctx.fillText(prefix+(i+1),x(p.enthalpyKjKg)+8,y(p.logPressure)-7)})}
function check(r){if(!r.ok)throw new Error(`${r.status} ${r.statusText}`);return r}
function escapeHtml(value){const el=document.createElement('span');el.textContent=value;return el.innerHTML}
init().catch(e=>{document.querySelector('#empty').textContent='加载失败：'+e.message});

