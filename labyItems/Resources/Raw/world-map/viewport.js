// Pure viewport math, shared by the renderer and node tests.
class MapViewport {
 constructor(mapWidth, mapHeight) { this.mapWidth=mapWidth; this.mapHeight=mapHeight; this.scale=0; this.x=0; this.y=0; this.maxZoom=12; }
 resize(width,height) {
  const center=this.scale ? this.toMap(this.width/2,this.height/2) : null;
  const zoom=this.scale ? this.scale/this.min : 1;
  this.width=width;this.height=height;this.min=Math.min(width/this.mapWidth,height/this.mapHeight);
  this.scale=this.min*zoom;
  if(center){this.x=width/2-center.x*this.scale;this.y=height/2-center.y*this.scale;this.clamp();}else this.fit();
 }
 fit(){this.scale=this.min;this.x=(this.width-this.mapWidth*this.scale)/2;this.y=(this.height-this.mapHeight*this.scale)/2;}
 toMap(x,y){return {x:(x-this.x)/this.scale,y:(y-this.y)/this.scale};}
 toScreen(x,y){return {x:x*this.scale+this.x,y:y*this.scale+this.y};}
 zoomAt(factor,x,y){const anchor=this.toMap(x,y);this.scale=Math.max(this.min,Math.min(this.min*this.maxZoom,this.scale*factor));this.x=x-anchor.x*this.scale;this.y=y-anchor.y*this.scale;this.clamp();}
 pan(dx,dy){this.x+=dx;this.y+=dy;this.clamp();}
 clamp(){for(const [axis,size,map] of [['x',this.width,this.mapWidth],['y',this.height,this.mapHeight]]){const length=map*this.scale;this[axis]=length<=size?(size-length)/2:Math.max(size-length,Math.min(0,this[axis]));}}
}
if(typeof module!=='undefined')module.exports={MapViewport};
