
const page=await figma.getNodeByIdAsync('0:1');await figma.setCurrentPageAsync(page);
const entries=[
 ['7:32',0,.16,8],['7:36',.04,.14,4],
 ['7:39',.06,.16,6],['7:73',.09,.16,6],['7:97',.12,.16,6],
 ['7:122',.10,.14,4],['7:125',.12,.16,6],['7:149',.15,.16,6],['7:173',.18,.16,6],
 ['7:205',.14,.16,4],
 ['7:369',.04,.20,6],['7:385',.08,.20,6],['7:406',.12,.20,6],['7:417',.08,.20,6]
];
const nodes=await Promise.all(entries.map(async e=>({node:await figma.getNodeByIdAsync(e[0]),start:e[1],duration:e[2],offset:e[3]})));
const panel=await figma.getNodeByIdAsync('7:212');
const optionGroup=await figma.getNodeByIdAsync('I7:212;6:125');
const previewGroup=await figma.getNodeByIdAsync('I7:420;6:125');
if(optionGroup.children.length!==4)throw new Error('The opening panel no longer has four software options');
const targets=[...nodes.map(x=>x.node),panel,...optionGroup.children,previewGroup.children[0]];
const fonts=new Map();for(const n of targets){const texts=n.type==='TEXT'?[n]:'findAllWithCriteria'in n?n.findAllWithCriteria({types:['TEXT']}):[];for(const t of texts)for(const seg of t.getStyledTextSegments(['fontName']))fonts.set(JSON.stringify(seg.fontName),seg.fontName);}
await Promise.all([...fonts.values()].map(f=>figma.loadFontAsync(f)));
const mutated=new Set(),written=[],preserved=[];
const easing={type:'CUSTOM_CUBIC_BEZIER',easingFunctionCubicBezier:{x1:.2,y1:0,x2:0,y2:1}};
function track(n,name,values){
 if(n.manualKeyframeTracks?.[name]){preserved.push({id:n.id,field:name});return;}
 n.applyManualKeyframeTrack({type:'PROPERTY',name},{keyframes:values.map(([t,v],i)=>({timelinePosition:t,value:{type:'FLOAT',value:v},...(i?{easing}:{} )}))});
 mutated.add(n.id);written.push({id:n.id,field:name,keyframes:values});
}
for(const {node,start,duration,offset}of nodes){
 if(!node)throw new Error('Missing A motion target');
 track(node,'OPACITY',[[start,0],[start+duration,1]]);
 track(node,'TRANSLATION_Y',[[start,offset],[start+duration,0]]);
}
track(panel,'OPACITY',[[0,0],[.18,1]]);
track(panel,'TRANSLATION_X',[[0,-12],[.22,0]]);
const timeline=panel.timelines[0];if(timeline.duration<.46){panel.setTimelineDuration(timeline.id,.46);mutated.add(timeline.id);}
return {mutatedNodeIds:[...mutated],newTracks:written.length,writtenTracks:written,preservedTracks:preserved,timelines:panel.timelines,finalValues:targets.filter(n=>mutated.has(n.id)).map(n=>({id:n.id,name:n.name,fields:Object.keys(n.manualKeyframeTracks)})),fonts:[...fonts.values()],staticDesignUnchanged:true,scope:'A only'};

