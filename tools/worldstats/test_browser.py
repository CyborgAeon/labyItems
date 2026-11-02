#!/usr/bin/env python3
"""Run SVG containment and gesture integration checks in installed headless Chrome."""
import pathlib
import subprocess
import tempfile

root=pathlib.Path(__file__).resolve().parents[2]
assets=root/'labyItems/Resources/Raw/world-map'
svg='''<svg xmlns="http://www.w3.org/2000/svg"><g transform="translate(100,100)">
<path id="concave" data-type="region" d="M0 0H100V30H30V100H0Z"/>
<path id="curve" data-type="region" d="M200 0C300 0 300 100 200 100C100 100 100 0 200 0Z"/>
<path id="location" data-type="location" d="M190 40H210V60H190Z"/>
<path id="z-overlap" data-type="region" d="M400 0H500V100H400Z"/>
<path id="a-overlap" data-type="region" d="M400 0H500V100H400Z"/>
</g></svg>'''
checks='''
const results=[],notifications=[];
notify=url=>notifications.push(url);
function check(name,condition){if(!condition)throw Error(name);results.push(name);}
function hit(x,y){return hitTest(viewport.toScreen(x,y))?.id;}
try{
 viewport.resize(800,600);draw();
 check('concave inside',hit(110,180)==='concave');
 check('concave notch excluded',hit(180,180)===undefined);
 check('Bezier inside',hit(300,120)==='curve');
 check('Bezier bounding corner excluded',hit(226,101)===undefined);
 check('locations precede regions',hit(300,150)==='location');
 check('stable ID resolves identical overlaps',hit(550,150)==='a-overlap');
 viewport.zoomAt(4,400,300);viewport.pan(-50,-20);draw();
 check('transformed hit after pan and zoom',hit(300,150)==='location');
 svg.setPointerCapture=()=>{};
 const send=(type,id,x,y)=>svg.dispatchEvent(new PointerEvent(type,{pointerId:id,clientX:x,clientY:y,bubbles:true}));
 send('pointerdown',1,100,100);send('pointermove',1,130,100);send('pointermove',1,100,100);
 check('returning drag remains a drag',dragged);send('pointerup',1,100,100);
 send('pointerdown',1,100,100);send('pointerdown',2,200,100);send('pointerup',2,200,100);
 check('pinch stays suppressed after one finger lifts',pinched);send('pointerup',1,100,100);
 check('drag and pinch do not select',selected===null);
 check('drag and pinch never request a selection',!notifications.some(url=>url.startsWith('primus://select/')));
 const before={x:viewport.x,y:viewport.y,scale:viewport.scale};
 const tap=viewport.toScreen(300,150);
 send('pointerdown',3,tap.x,tap.y);send('pointerup',3,tap.x,tap.y);
 check('tap selects path for the name tooltip',selected?.id==='location');
 check('tap bridge includes normalized tooltip anchor',notifications.at(-1).startsWith('primus://select/location?x='));
 check('tap does not request details',!notifications.some(url=>url.includes('details')));
 check('tap preserves viewport',viewport.x===before.x&&viewport.y===before.y&&viewport.scale===before.scale);
 document.querySelector('#clear').click();
 check('clear removes selection and tooltip',selected===null&&notifications.at(-1)==='primus://hide');

 document.querySelector('#result').textContent='PASS '+results.length+' '+results.join('; ');
}catch(e){document.querySelector('#result').textContent='FAIL '+e.stack;}
'''
html='<style>#map{width:800px;height:600px}svg{width:100%;height:100%}body{margin:0}</style><div id="map">'+svg+'</div><nav>'+''.join('<button id="'+x+'"></button>' for x in ['plus','minus','fit','clear'])+'</nav><pre id="result"></pre><script>const background="";'+(assets/'viewport.js').read_text()+(assets/'map.js').read_text()+checks+'</script>'
with tempfile.TemporaryDirectory(prefix='primus-browser-') as directory:
 path=pathlib.Path(directory)/'test.html';path.write_text(html)
 result=subprocess.run(['/Applications/Google Chrome.app/Contents/MacOS/Google Chrome','--headless','--no-sandbox','--disable-gpu','--user-data-dir='+directory+'/profile','--dump-dom',path.as_uri()],capture_output=True,text=True,timeout=40)
 import re
 match=re.search(r'<pre id="result">(.*?)</pre>',result.stdout,re.S)
 if not match:raise SystemExit('Browser did not return test results: '+result.stderr[:1500])
 print(match.group(1))
 if not match.group(1).startswith('PASS '):raise SystemExit(1)
