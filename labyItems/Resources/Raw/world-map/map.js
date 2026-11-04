const svg=document.querySelector('svg'), host=document.querySelector('#map');
const ns='http://www.w3.org/2000/svg';
function notify(url){location.href=url;}
const layer=document.createElementNS(ns,'g');
// Move the original nodes together, preserving all ancestor transforms and metadata.
while(svg.firstChild)layer.appendChild(svg.firstChild);
svg.appendChild(layer);svg.removeAttribute('viewBox');svg.removeAttribute('width');svg.removeAttribute('height');
if(background){const image=document.createElementNS(ns,'image');image.setAttribute('href',background);image.setAttribute('width','2048');image.setAttribute('height','1536');image.setAttribute('preserveAspectRatio','none');layer.prepend(image);document.body.classList.add('artwork');}
const paths=[...svg.querySelectorAll('path[data-type]')];
const viewport=new MapViewport(2048,1536), pointers=new Map();
let dragged=false,pinched=false,start=null,selected=null,tooltipHidden=false;
const draw=()=>layer.setAttribute('transform',`translate(${viewport.x} ${viewport.y}) scale(${viewport.scale})`);
new ResizeObserver(()=>{viewport.resize(host.clientWidth,host.clientHeight);draw();}).observe(host);
const point=e=>{const r=host.getBoundingClientRect();return{x:e.clientX-r.left,y:e.clientY-r.top};};
const distance=(a,b)=>Math.hypot(a.x-b.x,a.y-b.y);
const midpoint=(a,b)=>({x:(a.x+b.x)/2,y:(a.y+b.y)/2});
svg.addEventListener('pointerdown',e=>{e.preventDefault();svg.setPointerCapture(e.pointerId);const p=point(e);pointers.set(e.pointerId,p);if(pointers.size===1){tooltipHidden=false;start=p;dragged=false;pinched=false;}else pinched=true;});
svg.addEventListener('pointermove',e=>{
 if(!pointers.has(e.pointerId))return;
 const p=point(e),old=pointers.get(e.pointerId);
 if(!tooltipHidden&&(pointers.size>1||distance(p,start)>8)){notify('primus://hide');tooltipHidden=true;}
 if(pointers.size===1){if(distance(p,start)>8)dragged=true;viewport.pan(p.x-old.x,p.y-old.y);}
 else if(pointers.size===2){const before=[...pointers.values()];const midBefore=midpoint(...before);const d=distance(...before);pointers.set(e.pointerId,p);const after=[...pointers.values()],midAfter=midpoint(...after);if(d>0)viewport.zoomAt(distance(...after)/d,midBefore.x,midBefore.y);viewport.pan(midAfter.x-midBefore.x,midAfter.y-midBefore.y);}
 pointers.set(e.pointerId,p);draw();
});
function hitTest(p){
 // Locations first, then smallest region bounding area, finally stable SVG ID.
 // The bounding area orders hits only; containment uses the exact SVG geometry.
 return paths.filter(path=>{const matrix=path.getScreenCTM();if(!matrix)return false;const local=new DOMPoint(p.x+host.getBoundingClientRect().left,p.y+host.getBoundingClientRect().top).matrixTransform(matrix.inverse());return path.isPointInFill(local);})
 .sort((a,b)=>{const rank=x=>x.dataset.type==='location'?0:1;const area=x=>{const r=x.getBBox();return r.width*r.height;};return rank(a)-rank(b)||area(a)-area(b)||(a.id<b.id?-1:a.id>b.id?1:0);})[0];
}
svg.addEventListener('pointerup',e=>{if(!pointers.has(e.pointerId))return;const tap=!dragged&&!pinched&&distance(point(e),start)<=8;pointers.delete(e.pointerId);if(tap){const path=hitTest(point(e));if(!path){notify('primus://hide');}if(path){if(selected)selected.classList.remove('chosen');selected=path;path.classList.add('chosen');const p=point(e);notify('primus://select/'+encodeURIComponent(path.id)+'?x='+(p.x/host.clientWidth)+'&y='+(p.y/host.clientHeight));}}});
svg.addEventListener('pointercancel',e=>{pointers.delete(e.pointerId);pinched=true;});
svg.addEventListener('wheel',e=>{e.preventDefault();notify('primus://hide');const p=point(e);viewport.zoomAt(Math.exp(-e.deltaY*.002),p.x,p.y);draw();},{passive:false});
document.querySelector('#plus').onclick=()=>{notify('primus://hide');viewport.zoomAt(1.5,viewport.width/2,viewport.height/2);draw();};
document.querySelector('#minus').onclick=()=>{notify('primus://hide');viewport.zoomAt(1/1.5,viewport.width/2,viewport.height/2);draw();};
document.querySelector('#fit').onclick=()=>{notify('primus://hide');viewport.fit();draw();};
document.querySelector('#clear').onclick=()=>{notify('primus://hide');if(selected)selected.classList.remove('chosen');selected=null;};

// Native controls and town results share the renderer's existing viewport.
function selectRegion(id,focus){
 if(selected)selected.classList.remove('chosen');
 selected=paths.find(path=>path.id===id)||null;
 if(!selected)return;
 selected.classList.add('chosen');
 if(focus){
  const box=selected.getBBox(),matrix=selected.getCTM();
  const parent=layer.getCTM();
  const center=new DOMPoint(box.x+box.width/2,box.y+box.height/2).matrixTransform(matrix).matrixTransform(parent.inverse());
  const targetScale=Math.min(viewport.min*4,viewport.min*viewport.maxZoom);
  viewport.scale=targetScale;
  viewport.x=viewport.width/2-center.x*targetScale;
  viewport.y=viewport.height/2-center.y*targetScale;
  viewport.clamp();draw();
 }
}
function setMatchingRegions(ids){
 const matching=ids===null?null:new Set(ids);
 for(const path of paths)path.classList.toggle('matching',matching!==null&&matching.has(path.id));
}
