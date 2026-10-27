
const page=await figma.getNodeByIdAsync('3:2');await figma.setCurrentPageAsync(page);
const ids=['6:126','6:136','6:146','6:156'];const rows=await Promise.all(ids.map(id=>figma.getNodeByIdAsync(id)));
const fonts=new Map();for(const n of rows)for(const t of n.findAllWithCriteria({types:['TEXT']}))for(const seg of t.getStyledTextSegments(['fontName']))fonts.set(JSON.stringify(seg.fontName),seg.fontName);await Promise.all([...fonts.values()].map(f=>figma.loadFontAsync(f)));
const easing={type:'CUSTOM_CUBIC_BEZIER',easingFunctionCubicBezier:{x1:.2,y1:0,x2:0,y2:1}};
const mutated=[];rows.forEach((n,i)=>{const start=.08+i*.03;
 for(const [name,from,to]of [['OPACITY',0,1],['TRANSLATION_X',-4,0]]){
  if(n.manualKeyframeTracks?.[name])throw new Error('Preserving existing '+name+' track');
  n.applyManualKeyframeTrack({type:'PROPERTY',name},{keyframes:[{timelinePosition:start,value:{type:'FLOAT',value:from}},{timelinePosition:start+.14,value:{type:'FLOAT',value:to},easing}]});
 }mutated.push(n.id);
});
const selected=rows[0];selected.applyManualKeyframeTrack({type:'INDEXED_ITEM',collection:'fills',index:0},{keyframes:[{timelinePosition:.32,value:{type:'COLOR',value:{r:1,g:1,b:1,a:1}}},{timelinePosition:.46,value:{type:'COLOR',value:{r:232/255,g:240/255,b:1,a:1}},easing}]});
return {mutatedNodeIds:mutated,newTracks:9,rows:rows.map(n=>({id:n.id,manualKeyframeTracks:n.manualKeyframeTracks})),timeline:rows[0].timelines,scope:'Only A OpeningPanel component; no static or B changes'};

