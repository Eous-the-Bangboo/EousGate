
await Promise.all(['Regular','Medium','Bold'].map(style=>figma.loadFontAsync({family:'Noto Sans SC',style})));
const page=await figma.getNodeByIdAsync("3:2");await figma.setCurrentPageAsync(page);
const tools=await figma.getNodeByIdAsync("5:120");
const mutated=[];for(const n of tools.findAll(n=>n.type==='FRAME'&&n.name==='Title and identifier')){n.counterAxisSizingMode='AUTO';mutated.push(n.id);}
const brand=await figma.getNodeByIdAsync("4:35");
const old=await figma.getNodeByIdAsync('4:36');old.remove();
const svg=figma.createNodeFromSvg("<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"256\" height=\"256\" viewBox=\"0 0 256 256\">\n  <title>EousGate</title>\n  <rect x=\"8\" y=\"8\" width=\"240\" height=\"240\" rx=\"58\" fill=\"#0B1220\"/>\n  <path d=\"M62 210V117c0-37 30-67 66-67s66 30 66 67v93\" fill=\"none\" stroke=\"#38BDF8\" stroke-width=\"24\" stroke-linecap=\"round\"/>\n  <path d=\"M86 210v-91c0-23 19-42 42-42s42 19 42 42v91\" fill=\"#0B1220\" stroke=\"#E0F2FE\" stroke-width=\"14\" stroke-linejoin=\"round\"/>\n  <path d=\"M44 176h168\" fill=\"none\" stroke=\"#F59E0B\" stroke-width=\"18\" stroke-linecap=\"round\"/>\n  <path d=\"M116 176h24\" fill=\"none\" stroke=\"#FEF3C7\" stroke-width=\"8\" stroke-linecap=\"round\"/>\n</svg>\n");svg.rescale(24/svg.width);brand.appendChild(svg);svg.x=0;svg.y=0;svg.constraints={horizontal:'SCALE',vertical:'SCALE'};for(const n of svg.findAll(()=>true))if('constraints'in n)n.constraints={horizontal:'SCALE',vertical:'SCALE'};
mutated.push(brand.id);
const focus=figma.createEffectStyle();focus.name='Eous/A/FocusRing';focus.effects=[{type:'DROP_SHADOW',color:{r:1,g:1,b:1,a:1},offset:{x:0,y:0},radius:0,spread:2,visible:true,blendMode:'NORMAL'},{type:'DROP_SHADOW',color:{r:37/255,g:99/255,b:235/255,a:1},offset:{x:0,y:0},radius:0,spread:4,visible:true,blendMode:'NORMAL'}];
const buttons=await figma.getNodeByIdAsync("4:90");for(const n of buttons.children.filter(n=>n.name.endsWith('State=Focus'))){n.effectStyleId=focus.id;mutated.push(n.id);}
return {createdNodeIds:[svg.id,...svg.findAll(()=>true).map(n=>n.id)],mutatedNodeIds:mutated,removedNodeIds:['4:36'],focusStyle:focus.id,titleRows:tools.children.map(c=>({state:c.name,rowHeight:c.height,titleRow:c.findOne(n=>n.name==='Title and identifier').height}))};

