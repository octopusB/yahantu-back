const $=selector=>document.querySelector(selector);
if(!window.echarts){$('#message').className='message error';$('#message').textContent='ECharts 未加载，请确认 frontend/vendor/echarts.min.js 存在。';throw new Error('ECharts is unavailable')}
const chart=echarts.init($('#chart'),null,{renderer:'canvas'});let timer=null,lastData=null;
const api=path=>$('#apiBase').value.trim().replace(/\/$/,'')+path;

$('#apiBase').value=localStorage.getItem('ph-api-base')||$('#apiBase').value;
$('#connect').onclick=connect;
$('#refrigerant').onchange=async()=>{await request('/api/refrigerant',{method:'POST',headers:{'content-type':'application/json'},body:JSON.stringify({refrigerant:$('#refrigerant').value})});await refresh()};
$('#autoRefresh').onchange=configureTimer;
$('#chartWidth').oninput=e=>resizeChart(e.target.value,null);
$('#chartHeight').oninput=e=>resizeChart(null,e.target.value);
window.addEventListener('resize',()=>chart.resize());

async function connect(){
  try{
    localStorage.setItem('ph-api-base',$('#apiBase').value.trim());
    const names=await request('/api/refrigerants').then(r=>r.json());
    $('#refrigerant').innerHTML=names.map(x=>`<option>${escapeHtml(x)}</option>`).join('');$('#refrigerant').disabled=false;
    await refresh();setState(true);configureTimer();
  }catch(error){setState(false,error.message)}
}
async function refresh(){
  try{
    const data=await request('/api/diagram').then(r=>r.json());lastData=data;render(data);setState(!data.error,data.error||'已连接');
  }catch(error){setState(false,error.message)}
}
async function request(path,options){const response=await fetch(api(path),options);if(!response.ok)throw new Error(`${response.status} ${response.statusText}`);return response}
function configureTimer(){clearInterval(timer);timer=null;if($('#autoRefresh').checked)timer=setInterval(refresh,1000)}
function setState(online,message){$('#connectionState').className=`state ${online?'online':'offline'}`;$('#connectionState').textContent=online?'已连接':'未连接';$('#message').className=`message${online?'':' error'}`;$('#message').textContent=message||''}
function resizeChart(width,height){if(width){$('#chartShell').style.width=`${width}%`;$('#widthValue').textContent=`${width}%`}if(height){$('#chart').style.height=`${height}px`;$('#heightValue').textContent=`${height}px`}requestAnimationFrame(()=>chart.resize())}

function render(data){
  $('#refrigerant').value=data.refrigerant;$('#backend').textContent=data.propertyBackend;$('#updated').textContent=new Date(data.updatedAt).toLocaleTimeString();
  const c=data.conditions;$('#conditions').textContent=`标准工况：蒸发 ${c.evaporatingTemperatureC} °C / 冷凝 ${c.condensingTemperatureC} °C / 过热 ${c.superheatK} K / 过冷 ${c.subcoolingK} K`;
  renderTable(data);chart.setOption(buildOption(data),true);
}
function point(p){return{value:[p.enthalpyKjKg,p.logPressure,p.temperatureC,p.pressureMpa],name:p.pointName}}
function curvePoint(p,name){return{value:[p.enthalpyKjKg,p.logPressure,p.temperatureC,p.pressureMpa],name}}
function buildOption(data){
  const measured=data.measuredPoints.map(point),standard=data.standardCycle.map(point);
  return{
    backgroundColor:'transparent',animationDuration:350,color:['#55a9ff','#e8759d','#ffb454','#47d7c8'],
    title:{text:`${data.refrigerant}  lgP-h`,left:24,top:16,textStyle:{color:'#eaf3f7',fontSize:17,fontWeight:600}},
    legend:{top:18,right:120,textStyle:{color:'#a8bac4'},data:['饱和液线','饱和蒸汽线','标准循环','实测循环']},
    toolbox:{right:18,top:10,feature:{dataZoom:{yAxisIndex:'none'},restore:{},saveAsImage:{name:`${data.refrigerant}-lgP-h`,pixelRatio:2}}},
    tooltip:{trigger:'item',confine:true,backgroundColor:'#08131deF',borderColor:'#355466',textStyle:{color:'#edf7fa'},formatter:p=>{const v=p.value;return `<b>${escapeHtml(p.seriesName)} · ${escapeHtml(p.name||'曲线点')}</b><br>T = ${Number(v[2]).toFixed(2)} °C<br>P = ${Number(v[3]).toFixed(4)} MPa(abs)<br>lgP = ${Number(v[1]).toFixed(4)}<br>h = ${Number(v[0]).toFixed(2)} kJ/kg`}},
    grid:{left:76,right:35,top:72,bottom:82,containLabel:true},
    xAxis:{type:'value',name:'比焓 h (kJ/kg)',nameLocation:'middle',nameGap:42,scale:true,axisLine:{lineStyle:{color:'#607d8b'}},axisLabel:{color:'#8fa5b1'},splitLine:{lineStyle:{color:'#1b3443'}}},
    yAxis:{type:'value',name:'lg P（P: MPa 绝对压力）',nameLocation:'middle',nameGap:52,scale:true,axisLine:{lineStyle:{color:'#607d8b'}},axisLabel:{color:'#8fa5b1'},splitLine:{lineStyle:{color:'#1b3443'}}},
    dataZoom:[{type:'inside',xAxisIndex:0,yAxisIndex:0,filterMode:'none',zoomOnMouseWheel:true,moveOnMouseMove:true},{type:'slider',xAxisIndex:0,height:18,bottom:18,borderColor:'#294554',fillerColor:'#2d7c8055',textStyle:{color:'#8fa5b1'}}],
    series:[
      {name:'饱和液线',type:'line',showSymbol:false,smooth:.15,lineStyle:{width:2},data:data.saturatedLiquid.map((p,i)=>curvePoint(p,`饱和液 ${i+1}`))},
      {name:'饱和蒸汽线',type:'line',showSymbol:false,smooth:.15,lineStyle:{width:2},data:data.saturatedVapor.map((p,i)=>curvePoint(p,`饱和汽 ${i+1}`))},
      {name:'标准循环',type:'line',symbol:'diamond',symbolSize:10,lineStyle:{width:2,type:'dashed'},data:[...standard,standard[0]].filter(Boolean)},
      {name:'实测循环',type:'line',symbol:'circle',symbolSize:12,lineStyle:{width:3},itemStyle:{borderColor:'#fff',borderWidth:1},data:[...measured,measured[0]].filter(Boolean)}
    ]
  };
}
function renderTable(data){
  const rows=(items,kind,label)=>items.map((p,i)=>`<tr><td><i class="dot ${kind}"></i>${label}${i+1}</td><td>${escapeHtml(p.pointName)}</td><td>${p.temperatureC.toFixed(2)}</td><td>${p.pressureMpa.toFixed(4)}</td><td>${p.logPressure.toFixed(4)}</td><td>${p.enthalpyKjKg.toFixed(2)}</td></tr>`).join('');
  $('#values').innerHTML=rows(data.measuredPoints,'measured','M')+rows(data.standardCycle,'standard','S');
}
function escapeHtml(value){const el=document.createElement('span');el.textContent=String(value);return el.innerHTML}

