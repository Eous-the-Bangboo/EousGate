/**
 * createVariableCollection
 *
 * Creates a new Figma variable collection with the specified name and modes.
 * If `modeNames` has more than one entry, the first mode is renamed from
 * Figma's default "Mode 1" to the first name, and additional modes are added.
 *
 * @param {string} name - The display name of the collection (e.g. "Color", "Spacing").
 * @param {string[]} modeNames - Ordered list of mode names (e.g. ["Light", "Dark"] or ["Value"]).
 * @returns {Promise<{
 *   collection: VariableCollection,
 *   modeIds: Record<string, string>
 * }>}
 *   `modeIds` maps each mode name to its modeId string.
 */
async function createVariableCollection(name, modeNames) {
  if (!modeNames || modeNames.length === 0) {
    throw new Error('createVariableCollection: modeNames must have at least one entry.')
  }

  // Create the collection — Figma always creates it with one mode named "Mode 1".
  const collection = figma.variables.createVariableCollection(name)

  // modeIds accumulator
  const modeIds = {}

  // Rename the default first mode
  const defaultMode = collection.modes[0]
  collection.renameMode(defaultMode.modeId, modeNames[0])
  modeIds[modeNames[0]] = defaultMode.modeId

  // Add additional modes
  for (let i = 1; i < modeNames.length; i++) {
    const newModeId = collection.addMode(modeNames[i])
    modeIds[modeNames[i]] = newModeId
  }

  return { collection, modeIds }
}

/**
 * createSemanticTokens
 *
 * Creates a batch of Figma variables in the given collection, one per entry in
 * `tokenMap`. Supports raw values, variable alias references, code syntax, and
 * scopes. Returns a map of token name → Variable for use in subsequent steps.
 *
 * @param {VariableCollection} collection - The target variable collection.
 * @param {Record<string, string>} modeIds - Map of {modeName: modeId} from createVariableCollection.
 * @param {Array<{
 *   name: string,
 *   type: 'COLOR' | 'FLOAT' | 'STRING' | 'BOOLEAN' | 'TIMING' | 'EASING',
 *   values: Record<string, string | number | boolean | MotionEasing | {type: 'VARIABLE_ALIAS', id: string}>,
 *   scopes?: VariableScope[],
 *   codeSyntax?: {WEB?: string, ANDROID?: string, iOS?: string}
 * }>} tokenMap - Ordered list of token definitions.
 *   - `name`: Variable name using slash hierarchy (e.g. "color/bg/primary").
 *   - `type`: Figma variable type.
 *   - `values`: Map of {modeName: value}. Values can be raw (hex string for COLOR,
 *     number for FLOAT) or alias objects {type: 'VARIABLE_ALIAS', id: variableId}.
 *     For COLOR, raw values are accepted as hex strings ("#rrggbb" or "#rrggbbaa")
 *     and converted to {r, g, b, a} automatically.
 *   - `scopes`: Array of VariableScope strings. Omit to use [] (hidden/primitive).
 *   - `codeSyntax`: Platform code syntax strings. Omit to skip.
 * @returns {Promise<{variables: Record<string, Variable>}>}
 *   `variables` maps each token name to its created Variable object.
 */
async function createSemanticTokens(collection, modeIds, tokenMap) {
  const variables = {}

  for (const token of tokenMap) {
    // Create the variable
    const variable = figma.variables.createVariable(token.name, collection, token.type)

    // Set values for each mode
    for (const [modeName, rawValue] of Object.entries(token.values)) {
      const modeId = modeIds[modeName]
      if (!modeId) {
        throw new Error(
          `createSemanticTokens: mode "${modeName}" not found in modeIds for token "${token.name}". ` +
            `Available modes: ${Object.keys(modeIds).join(', ')}`,
        )
      }

      let value = rawValue

      // Convert hex strings to Figma RGBA for COLOR type
      if (token.type === 'COLOR' && typeof rawValue === 'string' && rawValue.startsWith('#')) {
        value = hexToFigmaColor(rawValue)
      }

      variable.setValueForMode(modeId, value)
    }

    // Set scopes (default: empty array = hidden from property pickers / primitives)
    variable.scopes = token.scopes || []

    // Set code syntax per platform
    if (token.codeSyntax) {
      if (token.codeSyntax.WEB) {
        variable.setVariableCodeSyntax('WEB', token.codeSyntax.WEB)
      }
      if (token.codeSyntax.ANDROID) {
        variable.setVariableCodeSyntax('ANDROID', token.codeSyntax.ANDROID)
      }
      if (token.codeSyntax.iOS) {
        variable.setVariableCodeSyntax('iOS', token.codeSyntax.iOS)
      }
    }

    variables[token.name] = variable
  }

  return { variables }
}

/**
 * Converts a hex color string to a Figma RGBA object.
 * Supports "#rgb", "#rrggbb", and "#rrggbbaa".
 *
 * @param {string} hex
 * @returns {{ r: number, g: number, b: number, a: number }}
 */
function hexToFigmaColor(hex) {
  let h = hex.replace('#', '')

  // Expand shorthand #rgb → #rrggbb
  if (h.length === 3) {
    h = h
      .split('')
      .map((c) => c + c)
      .join('')
  }

  const r = parseInt(h.substring(0, 2), 16) / 255
  const g = parseInt(h.substring(2, 4), 16) / 255
  const b = parseInt(h.substring(4, 6), 16) / 255
  const a = h.length === 8 ? parseInt(h.substring(6, 8), 16) / 255 : 1

  return { r, g, b, a }
}


const pages = figma.root.children;
const review = pages.find(p=>p.id==='0:1'); review.name='A · 页面精修';
const components = figma.createPage(); components.name='A · 组件与状态';
await figma.setCurrentPageAsync(components);
await Promise.all(['Regular','Medium','Bold'].map(style=>figma.loadFontAsync({family:'Noto Sans SC',style})));
const {collection,modeIds}=await createVariableCollection('Eous / A 原生 Fluent',['Value']);
const raw={background:'#F4F6FA',foreground:'#172033',muted:'#526075',border:'#D5DCE6',accent:'#2563EB',hover:'#1D4ED8',accentSoft:'#E8F0FF',soft:'#EDF1F7',surface:'#FFFFFF',success:'#0F766E',successSoft:'#E5F4EE',danger:'#B42318',dangerSoft:'#FFEFED'};
const prim=(await createSemanticTokens(collection,modeIds,Object.entries(raw).map(([k,v])=>({name:'primitive/'+k,type:'COLOR',values:{Value:v},scopes:[]})))).variables;
const maps={PanelBackground:'background',PanelForeground:'foreground',PanelMutedForeground:'muted',PanelBorder:'border',PanelAccent:'accent',PanelAccentHover:'hover',PanelAccentSoft:'accentSoft',PanelSurfaceSoft:'soft',PanelElevatedSurface:'surface',PanelOnAccent:'surface',PanelSuccess:'success',PanelSuccessSoft:'successSoft',PanelDanger:'danger',PanelDangerSoft:'dangerSoft'};
const tokens=(await createSemanticTokens(collection,modeIds,Object.entries(maps).map(([k,v])=>({name:k,type:'COLOR',values:{Value:{type:'VARIABLE_ALIAS',id:prim['primitive/'+v].id}},scopes:k==='PanelBorder'?['STROKE_COLOR']:k.includes('Foreground')||k==='PanelOnAccent'?['TEXT_FILL']:['FRAME_FILL','SHAPE_FILL','TEXT_FILL'],codeSyntax:{WEB:'var(--'+k+')'}})))).variables;
for(const [k,v] of Object.entries(tokens))v.description='WPF: {DynamicResource '+k+'}';
const numbers={space4:4,space8:8,space12:12,space16:16,space24:24,space32:32,radius6:6,radius8:8,controlHeight:36,appRowHeight:60};
const metrics=(await createSemanticTokens(collection,modeIds,Object.entries(numbers).map(([k,v])=>({name:k,type:'FLOAT',values:{Value:v},scopes:k.startsWith('radius')?['CORNER_RADIUS']:k.includes('Height')?['WIDTH_HEIGHT']:['GAP'],codeSyntax:{WEB:'var(--'+k+')'}})))).variables;
const styles={};
for(const [key,size,line,weight] of [['Title',24,34,'Bold'],['Section',16,24,'Medium'],['Body',14,22,'Regular'],['BodyStrong',14,22,'Medium'],['Caption',12,18,'Regular'],['Micro',11,16,'Regular']]){const s=figma.createTextStyle();s.name='Eous/A/'+key;s.fontName={family:'Noto Sans SC',style:weight};s.fontSize=size;s.lineHeight={unit:'PIXELS',value:line};s.description='Figma preview font. Windows uses Segoe UI Variable Text / Microsoft YaHei UI.';styles[key]=s.id;}
const shadow=figma.createEffectStyle();shadow.name='Eous/A/Floating';shadow.effects=[{type:'DROP_SHADOW',color:{r:0.09,g:0.14,b:0.23,a:0.12},offset:{x:0,y:8},radius:28,spread:0,visible:true,blendMode:'NORMAL'}];
return {createdNodeIds:[components.id],mutatedNodeIds:[review.id],pages:{review:review.id,components:components.id},collectionId:collection.id,variables:Object.fromEntries(Object.entries({...tokens,...metrics}).map(([k,v])=>[k,v.id])),primitiveVariableIds:Object.values(prim).map(v=>v.id),styles,effect:shadow.id,counts:{variables:Object.keys(prim).length+Object.keys(tokens).length+Object.keys(metrics).length,textStyles:Object.keys(styles).length}};

