const {test}=require('node:test');
const assert=require('node:assert/strict');
const {MapViewport}=require('../../labyItems/Resources/Raw/world-map/viewport.js');
const near=(a,b)=>assert.ok(Math.abs(a-b)<1e-8,`${a} != ${b}`);
test('fit, coordinate round trip, and zoom limits',()=>{
 const v=new MapViewport(2048,1536);v.resize(400,800);
 near(v.scale,400/2048);near(v.x,0);near(v.y,250);
 const screen=v.toScreen(500,700),map=v.toMap(screen.x,screen.y);near(map.x,500);near(map.y,700);
 v.zoomAt(1000,200,400);near(v.scale,v.min*12);
 v.zoomAt(.0001,200,400);near(v.scale,v.min);near(v.y,250);
});
test('pinch keeps its midpoint map coordinate when unconstrained',()=>{
 const v=new MapViewport(2048,1536);v.resize(400,400);v.zoomAt(4,200,200);
 const before=v.toMap(180,210);v.zoomAt(1.5,180,210);const after=v.toMap(180,210);
 near(before.x,after.x);near(before.y,after.y);
});
test('pan boundaries and centering',()=>{
 const v=new MapViewport(2048,1536);v.resize(400,800);v.pan(10000,-10000);near(v.x,0);near(v.y,250);
 v.zoomAt(4,200,400);v.pan(10000,10000);near(v.x,0);near(v.y,0);
 v.pan(-10000,-10000);near(v.x,400-2048*v.scale);near(v.y,800-1536*v.scale);
});
test('rotation preserves center and relative zoom',()=>{
 const v=new MapViewport(2048,1536);v.resize(400,800);v.zoomAt(4,200,400);
 const before=v.toMap(200,400);v.resize(800,400);const after=v.toMap(400,200);
 near(before.x,after.x);near(before.y,after.y);near(v.scale/v.min,4);
});
