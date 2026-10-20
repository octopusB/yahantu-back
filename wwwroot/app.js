const canvas=document.querySelector('#chart'),ctx=canvas.getContext('2d'),select=document.querySelector('#refrigerant');
const palette=['#47d7c8','#ffb454','#e8759d','#88a7ff'];
async function init(){
  const names=await fetch('/api/refrigerants').then(r=>r.json());
  select.innerHTML=names.map(x=>`<option>${escapeHtml(x)}</option>`).join('');
  select.onchange=()=>fetch('/api/refrigerant',{method:'POST',headers:{'content-type':'application/json'},body:JSON.stringify({refrigerant:select.value})}).then(load);
  await load();setInterval(load,1000);addEventListener('resize',load);
}
async function load(){
  const data=await fetch('/api/diagram').then(r=>r.json());select.value=data.refrigerant;
  document.querySelector('#updated').textContent=data.updatedAt.startsWith('0001')?'尚未采样':new Date(data.updatedAt).toLocaleTimeString();
  document.querySelector('#empty').style.display=data.points.length?'none':'grid';
  document.querySelector('#cards').innerHTML=data.points.map((p,i)=>`<article class="card"><strong style="color:${palette[i%palette.length]}">${escapeHtml(p.pointName)}</strong><span>${p.temperatureC.toFixed(1)} °C</span><span>${p.pressureMpa.toFixed(3)} MPa</span><span>${p.enthalpyKjKg.toFixed(2)} kJ/kg</span></article>`).join('');
  draw(data.points);
}
function draw(points){
  const dpr=devicePixelRatio||1,w=canvas.clientWidth,h=canvas.clientHeight;canvas.width=w*dpr;canvas.height=h*dpr;ctx.setTransform(dpr,0,0,dpr,0,0);ctx.clearRect(0,0,w,h);
  const m={l:68,r:30,t:28,b:58},pw=w-m.l-m.r,ph=h-m.t-m.b,xMin=150,xMax=500,pMin=.2,pMax=4;
  const x=v=>m.l+(v-xMin)/(xMax-xMin)*pw,y=v=>m.t+(Math.log(pMax)-Math.log(v))/(Math.log(pMax)-Math.log(pMin))*ph;
  ctx.font='12px sans-serif';ctx.lineWidth=1;ctx.strokeStyle='#1d3544';ctx.fillStyle='#78909c';
  [150,200,250,300,350,400,450,500].forEach(v=>{ctx.beginPath();ctx.moveTo(x(v),m.t);ctx.lineTo(x(v),m.t+ph);ctx.stroke();ctx.fillText(v,x(v)-10,h-26)});
  [.2,.3,.5,.7,1,1.5,2,3,4].forEach(v=>{ctx.beginPath();ctx.moveTo(m.l,y(v));ctx.lineTo(m.l+pw,y(v));ctx.stroke();ctx.fillText(v.toFixed(1),22,y(v)+4)});
  ctx.fillStyle='#9ab0bc';ctx.fillText('h (kJ/kg)',w/2,h-7);ctx.save();ctx.translate(12,h/2);ctx.rotate(-Math.PI/2);ctx.fillText('P (MPa)',0,0);ctx.restore();
  if(!points.length)return;ctx.strokeStyle='#47d7c8';ctx.lineWidth=2.5;ctx.beginPath();points.forEach((p,i)=>i?ctx.lineTo(x(p.enthalpyKjKg),y(p.pressureMpa)):ctx.moveTo(x(p.enthalpyKjKg),y(p.pressureMpa)));ctx.closePath();ctx.stroke();
  points.forEach((p,i)=>{ctx.fillStyle=palette[i%palette.length];ctx.beginPath();ctx.arc(x(p.enthalpyKjKg),y(p.pressureMpa),6,0,Math.PI*2);ctx.fill();ctx.fillStyle='#eaf3f7';ctx.fillText(p.pointName,x(p.enthalpyKjKg)+10,y(p.pressureMpa)-8)});
}
function escapeHtml(value){const el=document.createElement('span');el.textContent=value;return el.innerHTML}
init().catch(e=>document.querySelector('#empty').textContent='加载失败：'+e.message);

