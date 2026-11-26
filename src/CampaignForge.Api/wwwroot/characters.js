const API='/api/characters', $=id=>document.getElementById(id);
const abilities=['strength','dexterity','constitution','intelligence','wisdom','charisma'];
const labels={strength:'STR',dexterity:'DEX',constitution:'CON',intelligence:'INT',wisdom:'WIS',charisma:'CHA'};
const classRules={Barbarian:{hd:12,bab:1,fort:2,ref:0,will:0,skills:4},Bard:{hd:6,bab:.75,fort:0,ref:2,will:2,skills:6},Cleric:{hd:8,bab:.75,fort:2,ref:0,will:2,skills:2},Druid:{hd:8,bab:.75,fort:2,ref:0,will:2,skills:4},Fighter:{hd:10,bab:1,fort:2,ref:0,will:0,skills:2},Monk:{hd:8,bab:.75,fort:2,ref:2,will:2,skills:4},Paladin:{hd:10,bab:1,fort:2,ref:0,will:0,skills:2},Ranger:{hd:8,bab:1,fort:2,ref:2,will:0,skills:6},Rogue:{hd:6,bab:.75,fort:0,ref:2,will:0,skills:8},Sorcerer:{hd:4,bab:.5,fort:0,ref:0,will:2,skills:2},Wizard:{hd:4,bab:.5,fort:0,ref:0,will:2,skills:2}};
const racial={Human:{},Dwarf:{constitution:2,charisma:-2},Elf:{dexterity:2,constitution:-2},Gnome:{constitution:2,strength:-2},'Half-Elf':{},'Half-Orc':{strength:2,intelligence:-2,charisma:-2},Halfling:{dexterity:2,strength:-2}};
const skills=[['Appraise','intelligence'],['Balance','dexterity'],['Bluff','charisma'],['Climb','strength'],['Concentration','constitution'],['Craft','intelligence'],['Decipher Script','intelligence'],['Diplomacy','charisma'],['Disable Device','intelligence'],['Disguise','charisma'],['Escape Artist','dexterity'],['Forgery','intelligence'],['Gather Information','charisma'],['Handle Animal','charisma'],['Heal','wisdom'],['Hide','dexterity'],['Intimidate','charisma'],['Jump','strength'],['Knowledge (arcana)','intelligence'],['Knowledge (architecture and engineering)','intelligence'],['Knowledge (dungeoneering)','intelligence'],['Knowledge (geography)','intelligence'],['Knowledge (history)','intelligence'],['Knowledge (local)','intelligence'],['Knowledge (nature)','intelligence'],['Knowledge (nobility and royalty)','intelligence'],['Knowledge (religion)','intelligence'],['Knowledge (the planes)','intelligence'],['Listen','wisdom'],['Move Silently','dexterity'],['Open Lock','dexterity'],['Perform','charisma'],['Profession','wisdom'],['Ride','dexterity'],['Search','intelligence'],['Sense Motive','wisdom'],['Sleight of Hand','dexterity'],['Speak Language','—'],['Spellcraft','intelligence'],['Spot','wisdom'],['Survival','wisdom'],['Swim','strength'],['Tumble','dexterity'],['Use Magic Device','charisma'],['Use Rope','dexterity']];
let currentStep=0,rollData=null,characters=[],drafts=[],editingId=null,skillValues={},skillMisc={},saveMisc={fort:0,ref:0,will:0},goldRollData=null,lastDraftTouch=0,draftActive=false,expectedUpdatedUtc=null,checkpointPage=0;
function notify(text,bad=false){$('notice').textContent=text;$('notice').className=bad?'message error':'message'}
async function api(url,opts={}){const r=await fetch(url,{...opts,headers:{'Content-Type':'application/json',...(opts.headers||{})}});if(!r.ok){let t=`${r.status} ${r.statusText}`;try{let j=await r.json();t=j.error||j.title||j.detail||t}catch{}throw Error(t)}return r.status===204?null:r.json()}
function abilityMod(n){return Math.floor((n-10)/2)}
function fillAbilitySelects(){for(const a of abilities){$(a==='strength'?'str':a==='dexterity'?'dex':a==='constitution'?'con':a==='intelligence'?'int':a==='wisdom'?'wis':'cha').innerHTML='<option value="">Assign score</option>'+Array.from({length:16},(_,i)=>`<option value="${i+3}">${i+3}</option>`).join('')}}
function assigned(){const out={};for(const a of abilities){const id={strength:'str',dexterity:'dex',constitution:'con',intelligence:'int',wisdom:'wis',charisma:'cha'}[a];const v=$(id).value;out[a]=v===''?0:(rollData?.rolls[Number(v)]?.total||0)}return out}
function renderRolls(){const host=$('abilityRolls');if(!rollData){host.innerHTML='<div class="roll-placeholder">Your six rolls will appear here.</div>';return}host.innerHTML=rollData.rolls.map((r,i)=>`<div class="ability-roll"><strong>${r.total}</strong><span>${r.dice.join(' · ')} <br>drop ${r.dropped}</span></div>`).join('');$('rollMethod').textContent='Server-rolled · 4d6 drop lowest';for(const a of abilities){const id={strength:'str',dexterity:'dex',constitution:'con',intelligence:'int',wisdom:'wis',charisma:'cha'}[a];$(id).innerHTML='<option value="">Assign score</option>'+rollData.rolls.map((r,i)=>`<option value="${i}">Roll ${i+1}: ${r.total}</option>`).join('')}}
function calc(){const cls=classRules[$('className').value],race=$('race').value,scores=assigned(),adj=racial[race]||{};const intScore=(scores.intelligence||10)+(adj.intelligence||0),intMod=abilityMod(intScore);const humanBonus=race==='Human'?4:0;const points=cls?Math.max(1,cls.skills+intMod)*4+humanBonus:0;
$('hitDie').textContent=cls?`d${cls.hd}`:'—';const conScore=(scores.constitution||10)+(adj.constitution||0);$('hitPoints').textContent=cls?Math.max(1,cls.hd+abilityMod(conScore)):'—';$('bab').textContent=cls?(cls.bab===1?'+1':cls.bab===.75?'+0':'+0'):'—';const saveRows=[['fort','constitution',cls?.fort||0],['ref','dexterity',cls?.ref||0],['will','wisdom',cls?.will||0]];
 saveRows.forEach(([save,ability,base])=>{const score=(scores[ability]||10)+(adj[ability]||0),abilityBonus=abilityMod(score),misc=Number(saveMisc[save]||0),total=base+abilityBonus+misc;$(`${save}Base`).textContent=cls?`+${base}`:'—';$(`${save}Ability`).textContent=cls?(abilityBonus>=0?'+':'')+abilityBonus:'—';$(`${save}Total`).textContent=cls?(total>=0?'+':'')+total:'—';$(`${save}Misc`).value=misc;});$('skillPoints').textContent=cls?points:'—';$('skillPointsAvailable').value=cls?points:0;
const racialText=Object.entries(adj).map(([k,v])=>`${k.slice(0,3).toUpperCase()} ${v>0?'+':''}${v}`).join(' · ');$('raceAdjustments').textContent=race?`Racial ability adjustments: ${racialText||'None'}. Human receives +4 skill points at 1st level.`:'Choose a race to see racial adjustments.';
renderSkills(points);renderSummary();}

const classSkillMap={
Barbarian:['Climb','Craft','Handle Animal','Intimidate','Jump','Listen','Ride','Survival','Swim'],
Bard:['Appraise','Balance','Bluff','Climb','Concentration','Decipher Script','Diplomacy','Disguise','Escape Artist','Gather Information','Hide','Jump','Knowledge (arcana)','Listen','Move Silently','Perform','Profession','Sense Motive','Sleight of Hand','Speak Language','Spellcraft','Swim','Tumble','Use Magic Device'],
Cleric:['Concentration','Craft','Diplomacy','Heal','Knowledge (arcana)','Knowledge (history)','Knowledge (religion)','Profession','Spellcraft'],
Druid:['Concentration','Craft','Diplomacy','Handle Animal','Heal','Knowledge (nature)','Listen','Profession','Ride','Spellcraft','Spot','Survival','Swim'],
Fighter:['Climb','Craft','Handle Animal','Intimidate','Jump','Ride','Swim'],
Monk:['Balance','Climb','Concentration','Craft','Diplomacy','Escape Artist','Hide','Jump','Knowledge (arcana)','Knowledge (religion)','Listen','Move Silently','Perform','Profession','Sense Motive','Spot','Swim','Tumble'],
Paladin:['Concentration','Craft','Diplomacy','Handle Animal','Heal','Knowledge (nobility and royalty)','Knowledge (religion)','Profession','Ride','Sense Motive'],
Ranger:['Climb','Concentration','Craft','Handle Animal','Heal','Hide','Jump','Knowledge (dungeoneering)','Knowledge (geography)','Knowledge (nature)','Listen','Move Silently','Search','Spot','Survival','Swim','Use Rope'],
Rogue:['Appraise','Balance','Bluff','Climb','Craft','Decipher Script','Diplomacy','Disable Device','Disguise','Escape Artist','Forgery','Gather Information','Hide','Intimidate','Jump','Knowledge (local)','Listen','Move Silently','Open Lock','Perform','Profession','Search','Sense Motive','Sleight of Hand','Spot','Swim','Tumble','Use Magic Device','Use Rope'],
Sorcerer:['Bluff','Concentration','Knowledge (arcana)','Profession','Spellcraft'],
Wizard:['Concentration','Craft','Decipher Script','Knowledge (arcana)','Knowledge (architecture and engineering)','Knowledge (geography)','Knowledge (history)','Knowledge (local)','Knowledge (nature)','Knowledge (nobility and royalty)','Knowledge (religion)','Knowledge (the planes)','Profession','Spellcraft']
};
let skillSort={key:'name',direction:1};
function skillClassSet(){return new Set(classSkillMap[$('className').value]||[])}
function renderSkills(total){
 const set=skillClassSet(), race=$('race').value, base=assigned(), adj=racial[race]||{};
 const rows=skills.map(([name,ability])=>{
  const isClass=set.has(name), ranks=Number(skillValues[name]||0), score=ability==='—'?10:(base[ability]||10)+(adj[ability]||0);
  const modifier=ability==='—'?0:abilityMod(score), misc=Number(skillMisc[name]||0), totalMod=modifier+ranks+misc;
  return {name,ability:ability==='—'?'—':labels[ability],isClass,ranks,modifier,misc,total:totalMod};
 });
 const key=skillSort.key, dir=skillSort.direction;
 rows.sort((a,b)=>{
  let av=a[key],bv=b[key];
  if(key==='name'||key==='ability') return String(av).localeCompare(String(bv))*dir;
  if(key==='class') return ((a.isClass===b.isClass)?0:(a.isClass?-1:1))*dir || a.name.localeCompare(b.name);
  return (Number(av)-Number(bv))*dir || a.name.localeCompare(b.name);
 });
 $('skillRows').innerHTML=rows.map(r=>`<tr>
  <td class="skill-class-cell"><span class="${r.isClass?'class-dot':'cross-dot'}" title="${r.isClass?'Class skill':'Cross-class skill'}">${r.isClass?'●':'○'}</span></td>
  <td>${escapeHtml(r.name)}</td><td>${r.ability}</td><td class="numeric">${r.modifier>=0?'+':''}${r.modifier}</td>
  <td><input type="number" min="0" step="${r.isClass?1:0.5}" max="${r.isClass?4:2}" data-skill="${escapeHtml(r.name)}" value="${r.ranks}"></td>
  <td><input type="number" step="1" class="misc-input" data-misc="${escapeHtml(r.name)}" value="${r.misc}"></td>
  <td class="numeric skill-total">${r.total>=0?'+':''}${r.total}</td></tr>`).join('');
 document.querySelectorAll('.sort-button').forEach(b=>{b.classList.toggle('sorted',b.dataset.sort===key);b.dataset.direction=b.dataset.sort===key?(dir===1?'ascending':'descending'):'unsorted'});
 $('skillRows').querySelectorAll('input[data-skill]').forEach(i=>i.addEventListener('change',()=>{
  const r=rows.find(x=>x.name===i.dataset.skill), cap=r.isClass?4:2;
  let v=Math.max(0,Math.min(cap,Number(i.value)||0));v=r.isClass?Math.floor(v):Math.round(v*2)/2;
  i.value=v;skillValues[i.dataset.skill]=v;renderSkills(total);
 }));
 $('skillRows').querySelectorAll('input[data-misc]').forEach(i=>i.addEventListener('change',()=>{
  skillMisc[i.dataset.misc]=Number(i.value)||0;renderSkills(total);
 }));
 updateSkillTotals(total);
}
function updateSkillTotals(total){
 const set=skillClassSet();
 const spent=Object.entries(skillValues).reduce((n,[name,ranks])=>n+Number(ranks||0)*(set.has(name)?1:2),0);
 $('skillPointsAssigned').value=spent;
 if(spent>total)notify(`You have assigned ${spent-total} more skill points than available. Cross-class ranks cost 2 points each.`,true);
}
document.addEventListener('click',e=>{
 const b=e.target.closest('.sort-button');if(!b)return;
 const key=b.dataset.sort;
 if(skillSort.key===key)skillSort.direction*=-1;else skillSort={key,direction:1};
 renderSkills(Number($('skillPointsAvailable').value)||0);
});
function renderSummary(){const s=assigned(),race=$('race').value,cls=$('className').value;const mod=Object.entries(s).map(([k,v])=>`${labels[k]} ${v||'—'} (${v?((abilityMod(v+(racial[race]?.[k]||0))>=0?'+':'')+abilityMod(v+(racial[race]?.[k]||0))):'—'})`).join(' · ');$('reviewSummary').innerHTML=`<p><strong>${escapeHtml($('charName').value||'Unnamed')}</strong> · ${escapeHtml(race||'Race TBD')} ${escapeHtml(cls||'Class TBD')}</p><p>${escapeHtml($('alignment').value||'Alignment TBD')} · ${escapeHtml($('deity').value||'No deity selected')}</p><p>${mod}</p><p>${escapeHtml($('prestigeGoal').value||'No prestige class goal selected')}</p>`}
function escapeHtml(v){return String(v).replace(/[&<>"']/g,c=>({'&':'&amp;','<':'&lt;','>':'&gt;','"':'&quot;',"'":'&#39;'}[c]))}
function pageUnlocked(n){
 if(n===0)return true;
 if(n===1)return Boolean($('charName').value.trim());
 if(n===2)return Boolean(rollData&&Object.values(assigned()).every(Boolean)&&new Set(['str','dex','con','int','wis','cha'].map(id=>$(id).value)).size===6);
 if(n===3)return Boolean($('race').value&&$('className').value&&$('alignment').value);
 if(n===4)return pageUnlocked(3);
 return false;
}
function refreshNavigator(){
 document.querySelectorAll('.step').forEach(x=>{
  const i=Number(x.dataset.step),unlocked=pageUnlocked(i);
  x.disabled=!unlocked;x.classList.toggle('locked',!unlocked);
  x.classList.toggle('active',i===currentStep);x.classList.toggle('done',i<currentStep&&unlocked);
  x.setAttribute('aria-current',i===currentStep?'step':'false');
 });
}
function showStep(n){
 const target=Math.max(0,Math.min(4,n));
 if(!pageUnlocked(target)){notify('That page is locked. Complete its prerequisite information first.',true);refreshNavigator();return false}
 currentStep=target;
 document.querySelectorAll('.wizard-step').forEach(x=>x.classList.toggle('active',Number(x.dataset.panel)===currentStep));
 refreshNavigator();
 $('previousStep').disabled=currentStep===0;$('nextStep').hidden=currentStep===4;$('saveCharacter').hidden=currentStep!==4;
 $('stepCounter').textContent=`Step ${currentStep+1} of 5`;
 if(currentStep===2||currentStep===3||currentStep===4)calc();
 return true;
}
async function resetForm(){
 editingId=null;goldRollData=null;expectedUpdatedUtc=null;checkpointPage=0;
 $('rollAbilities').disabled=false;$('rollGold').disabled=false;$('startingGold').readOnly=true;
 $('characterForm').reset();rollData=null;skillValues={};skillMisc={};saveMisc={fort:0,ref:0,will:0};fillAbilitySelects();renderRolls();
 $('startingGold').value=0;$('remainingGold').value=0;$('goldFormula').textContent='Choose a class, then roll its D&D 3.5 starting gold.';
 $('equipment').value='';$('gearNotes').value='';$('feats').value='';$('prestigeGoal').value='';$('prestigeNotes').value='';
 showStep(0);notify('Creating your character draft…');
 try{
  const draft=await api(`${API}/drafts`,{method:'POST'});
  editingId=draft.id;expectedUpdatedUtc=draft.updatedUtc;draftActive=true;lastDraftTouch=Date.now();
  notify(`Draft created. Character ID: ${draft.id}`);await loadDrafts();
 }catch(e){notify(`Could not create character draft: ${e.message}`,true)}
}
async function load(){
 try{
  characters=await api(API);renderList();
  const campaigns=await api('/api/campaigns');
  $('campaignId').innerHTML='<option value="">Unassigned character</option>'+campaigns.map(c=>`<option value="${c.id}">${escapeHtml(c.name)}</option>`).join('');
  await loadDrafts();
 }catch(e){notify(`Could not load character data: ${e.message}`,true)}
}
async function loadDrafts(){
 try{drafts=await api(`${API}/drafts`);$('draftList').innerHTML=drafts.map(d=>`<button type="button" data-draft-id="${d.id}">${escapeHtml(d.name||'Unfinished character')}<small>Draft · ${new Date(d.updatedUtc).toLocaleString()}</small></button>`).join('')||'<p class="muted">No drafts in progress.</p>'}
 catch(e){$('draftList').innerHTML='<p class="muted">Draft list unavailable.</p>'}
}
function renderList(){$('characterList').innerHTML=characters.map(c=>`<button type="button" data-id="${c.id}" class="${c.id===editingId?'selected':''}">${escapeHtml(c.name)}<small>${escapeHtml(c.race||'Race TBD')} · ${escapeHtml(c.className||'Class TBD')} · Level ${c.characterLevel}</small></button>`).join('')||'<p class="muted">No characters yet.</p>'}
async function resumeDraft(id){
 const c=await api(`${API}/${id}`),root=JSON.parse(c.characterDataJson||'{}'),d=root.checkpointData||root;
 editingId=c.id;draftActive=true;expectedUpdatedUtc=c.updatedUtc;checkpointPage=Number(root.checkpointPage||0);
 $('rollAbilities').disabled=false;$('rollGold').disabled=false;$('startingGold').readOnly=true;
 $('charName').value=d.charName||(c.name==='Unfinished character'?'':c.name);$('playerName').value=d.playerName||c.playerName||'';$('campaignId').value=d.campaignId||c.campaignId||'';
 $('concept').value=d.concept||'';$('background').value=d.background||'';$('diceSet').value=d.diceSet||'Emerald Leaf';
 $('race').value=d.race||c.race||'';$('className').value=d.className||c.className||'';$('alignment').value=d.alignment||c.alignment||'';$('deity').value=d.deity||c.deity||'';
 rollData=d.abilityRolls||null;goldRollData=d.startingGoldResult||null;renderRolls();
 const as=d.abilityAssignment||{};for(const [k,id2] of Object.entries({strength:'str',dexterity:'dex',constitution:'con',intelligence:'int',wisdom:'wis',charisma:'cha'})){if(rollData&&as[k]!==undefined)$(id2).value=as[k]}
 skillValues=d.skills||{};skillMisc=d.skillMisc||{};saveMisc=d.saveMisc||{fort:0,ref:0,will:0};saveMisc=d.saveMisc||{fort:0,ref:0,will:0};
 for(const [id2,key] of [['feats','feats'],['prestigeGoal','prestigeGoal'],['prestigeNotes','prestigeNotes'],['startingGold','startingGold'],['equipment','equipment'],['gearNotes','gearNotes']])$(id2).value=d[key]??(id2==='startingGold'?0:'');
 if(goldRollData){$('rollGold').disabled=true;$('startingGold').value=goldRollData.totalGold;$('goldFormula').textContent=`${goldRollData.dice.join(' + ')} × ${goldRollData.multiplier} gp = ${goldRollData.totalGold} gp`}
 if(rollData)$('rollAbilities').disabled=true;
 updateGoldBalance();calc();refreshNavigator();
 const available=[4,3,2,1,0].find(i=>pageUnlocked(i));showStep(Math.min(checkpointPage,available));
 renderList();await loadDrafts();notify(`Resumed ${c.name||'character draft'} from its last saved checkpoint.`);
}
function fillFrom(c){
 editingId=c.id;draftActive=false;expectedUpdatedUtc=c.updatedUtc||null;
 const d=JSON.parse(c.characterDataJson||'{}');$('rollAbilities').disabled=true;$('rollGold').disabled=true;$('startingGold').readOnly=true;
 $('charName').value=c.name;$('playerName').value=c.playerName||'';$('campaignId').value=c.campaignId||'';
 $('concept').value=d.concept||'';$('background').value=d.background||'';$('diceSet').value=d.diceSet||'Emerald Leaf';
 $('race').value=c.race||'';$('className').value=c.className||'';$('alignment').value=c.alignment||'';$('deity').value=c.deity||'';
 const a=JSON.parse(c.abilityScoresJson||'{}');for(const [k,id2] of Object.entries({strength:'str',dexterity:'dex',constitution:'con',intelligence:'int',wisdom:'wis',charisma:'cha'}))$(id2).value=a[k]||'';
 rollData=d.abilityRolls||null;goldRollData=d.startingGoldResult||null;renderRolls();
 if(rollData&&d.abilityAssignment){for(const [k,id2] of Object.entries({strength:'str',dexterity:'dex',constitution:'con',intelligence:'int',wisdom:'wis',charisma:'cha'}))$(id2).value=d.abilityAssignment[k]??''}
 skillValues=d.skills||{};skillMisc=d.skillMisc||{};saveMisc=d.saveMisc||{fort:0,ref:0,will:0};
 for(const [id2,key] of [['feats','feats'],['prestigeGoal','prestigeGoal'],['prestigeNotes','prestigeNotes'],['startingGold','startingGold'],['equipment','equipment'],['gearNotes','gearNotes']])$(id2).value=d[key]??(id2==='startingGold'?100:'');
 if(goldRollData){$('rollGold').disabled=true;$('startingGold').value=goldRollData.totalGold;$('goldFormula').textContent=`${goldRollData.dice.join(' + ')} × ${goldRollData.multiplier} gp = ${goldRollData.totalGold} gp`}
 updateGoldBalance();calc();showStep(0);renderList();notify(`Loaded ${c.name}. Changes are not saved until you select Save character.`);
}
async function saveCheckpoint(page){
 if(!draftActive||!editingId)return;
 const pageData={
  charName:$('charName').value,playerName:$('playerName').value,campaignId:$('campaignId').value,
  concept:$('concept').value,background:$('background').value,diceSet:$('diceSet').value,
  race:$('race').value,className:$('className').value,alignment:$('alignment').value,deity:$('deity').value,
  abilityRolls:rollData,abilityAssignment:{strength:$('str').value,dexterity:$('dex').value,constitution:$('con').value,intelligence:$('int').value,wisdom:$('wis').value,charisma:$('cha').value},
  skills:skillValues,skillMisc,saveMisc,feats:$('feats').value,prestigeGoal:$('prestigeGoal').value,prestigeNotes:$('prestigeNotes').value,
  startingGold:$('startingGold').value,startingGoldResult:goldRollData,equipment:$('equipment').value,gearNotes:$('gearNotes').value
 };
 const response=await api(`${API}/${editingId}/checkpoint`,{method:'PUT',body:JSON.stringify({page,name:$('charName').value,expectedUpdatedUtc,pageData})});
 expectedUpdatedUtc=response.updatedUtc;checkpointPage=page;await loadDrafts();
}
$('newCharacter').addEventListener('click',resetForm);$('characterList').addEventListener('click',e=>{const b=e.target.closest('button[data-id]');if(b){const c=characters.find(x=>x.id===b.dataset.id);if(c)fillFrom(c)}});$('draftList').addEventListener('click',async e=>{const b=e.target.closest('button[data-draft-id]');if(!b)return;try{await resumeDraft(b.dataset.draftId)}catch(err){notify(`Could not resume draft: ${err.message}`,true)}});document.querySelector('.stepper').addEventListener('click',async e=>{const b=e.target.closest('.step[data-step]');if(!b||b.disabled)return;const target=Number(b.dataset.step);try{await saveCheckpoint(currentStep);showStep(target)}catch(err){notify(`Checkpoint failed; navigation was not changed: ${err.message}`,true)}});
$('rollAbilities').addEventListener('click',async()=>{try{const r=await api(`${API}/roll-abilities?characterId=${editingId}`,{method:'POST'});rollData=r;$('rollAbilities').disabled=true;renderRolls();refreshNavigator();notify('Six ability scores rolled by the server and recorded to this character.')}catch(e){notify(`Roll failed: ${e.message}`,true)}});
$('previousStep').addEventListener('click',()=>showStep(currentStep-1));$('nextStep').addEventListener('click',()=>{if(currentStep===0&&!$('charName').value.trim()){notify('Enter a character name before continuing.',true);return}if(currentStep===1&&!rollData){notify('Roll ability scores before continuing.',true);return}if(currentStep===1&&Object.values(assigned()).some(x=>!x)){notify('Assign all six ability scores before continuing.',true);return}if(currentStep===1&&new Set(['str','dex','con','int','wis','cha'].map(id=>$(id).value)).size!==6){notify('Each of the six rolled scores must be assigned exactly once.',true);return}if(currentStep===2&&(!$('race').value||!$('className').value||!$('alignment').value)){notify('Choose a race, class, and alignment.',true);return}if(currentStep===2){const a=$('alignment').value,c=$('className').value;if(c==='Paladin'&&a!=='Lawful Good'){notify('A D&D 3.5 paladin must be lawful good.',true);return}if(c==='Monk'&&!a.startsWith('Lawful')){notify('A D&D 3.5 monk must be lawful.',true);return}if(['Barbarian','Bard'].includes(c)&&a.startsWith('Lawful')){notify(`A D&D 3.5 ${c.toLowerCase()} cannot be lawful.`,true);return}if(c==='Druid'&&!a.includes('Neutral')){notify('A D&D 3.5 druid must have a neutral component in their alignment.',true);return}}saveCheckpoint(currentStep).then(()=>showStep(currentStep+1)).catch(err=>notify(`Checkpoint failed; you remain on this page: ${err.message}`,true))});
$('className').addEventListener('change',calc);$('race').addEventListener('change',calc);$('int').addEventListener('change',calc);
 document.querySelectorAll('[data-save-misc]').forEach(i=>i.addEventListener('change',()=>{saveMisc[i.dataset.saveMisc]=Number(i.value)||0;calc()}));$('skillRows').addEventListener('input',()=>{});function updateGoldBalance(){const costs=$('equipment').value.split('\n').reduce((n,line)=>{const p=line.split('|');return n+(p.length>1?(Number(p[1].trim())||0):0)},0);$('remainingGold').value=(Number($('startingGold').value)||0)-costs}
$('rollGold').addEventListener('click',async()=>{const cls=$('className').value;if(!cls){notify('Choose a class before rolling starting gold.',true);return}try{const r=await api(`${API}/starting-gold/${encodeURIComponent(cls)}?characterId=${editingId}`,{method:'POST'});goldRollData=r;$('rollGold').disabled=true;$('startingGold').value=r.totalGold;$('remainingGold').value=r.totalGold;$('goldFormula').textContent=`${r.dice.join(' + ')} × ${r.multiplier} gp = ${r.totalGold} gp`;notify('Starting gold rolled and recorded to this character.')}catch(e){notify(e.message,true)}});$('startingGold').addEventListener('input',updateGoldBalance);$('equipment').addEventListener('input',updateGoldBalance);
$('characterForm').addEventListener('submit',async e=>{e.preventDefault();if(!rollData){notify('Roll ability scores before saving.',true);return}const scores=assigned();if(Object.values(scores).some(x=>!x)){notify('Assign all six ability scores.',true);return}const total=Number($('skillPointsAvailable').value)||0;const clsSkills=new Set(({'Barbarian':['Climb','Craft','Handle Animal','Intimidate','Jump','Listen','Ride','Survival','Swim'],'Bard':['Appraise','Balance','Bluff','Climb','Concentration','Decipher Script','Diplomacy','Disguise','Escape Artist','Gather Information','Hide','Jump','Knowledge (arcana)','Listen','Move Silently','Perform','Profession','Sense Motive','Sleight of Hand','Speak Language','Spellcraft','Swim','Tumble','Use Magic Device'],'Cleric':['Concentration','Craft','Diplomacy','Heal','Knowledge (arcana)','Knowledge (history)','Knowledge (religion)','Profession','Spellcraft'],'Druid':['Concentration','Craft','Diplomacy','Handle Animal','Heal','Knowledge (nature)','Listen','Profession','Ride','Spellcraft','Spot','Survival','Swim'],'Fighter':['Climb','Craft','Handle Animal','Intimidate','Jump','Ride','Swim'],'Monk':['Balance','Climb','Concentration','Craft','Diplomacy','Escape Artist','Hide','Jump','Knowledge (arcana)','Knowledge (religion)','Listen','Move Silently','Perform','Profession','Sense Motive','Spot','Swim','Tumble'],'Paladin':['Concentration','Craft','Diplomacy','Handle Animal','Heal','Knowledge (nobility and royalty)','Knowledge (religion)','Profession','Ride','Sense Motive'],'Ranger':['Climb','Concentration','Craft','Handle Animal','Heal','Hide','Jump','Knowledge (dungeoneering)','Knowledge (geography)','Knowledge (nature)','Listen','Move Silently','Search','Spot','Survival','Swim','Use Rope'],'Rogue':['Appraise','Balance','Bluff','Climb','Craft','Decipher Script','Diplomacy','Disable Device','Disguise','Escape Artist','Forgery','Gather Information','Hide','Intimidate','Jump','Knowledge (local)','Listen','Move Silently','Open Lock','Perform','Profession','Search','Sense Motive','Sleight of Hand','Spot','Swim','Tumble','Use Magic Device','Use Rope'],'Sorcerer':['Bluff','Concentration','Knowledge (arcana)','Profession','Spellcraft'],'Wizard':['Concentration','Craft','Decipher Script','Knowledge (arcana)','Knowledge (architecture and engineering)','Knowledge (geography)','Knowledge (history)','Knowledge (local)','Knowledge (nature)','Knowledge (nobility and royalty)','Knowledge (religion)','Knowledge (the planes)','Profession','Spellcraft']}[$('className').value]||[]));const spent=Object.entries(skillValues).reduce((n,[name,ranks])=>n+ranks*(clsSkills.has(name)?1:2),0);if(spent>total){notify('Skill point allocation exceeds the available total.',true);return}const purchases=$('equipment').value.split('\n').map(line=>{const parts=line.split('|');return {item:(parts[0]||'').trim(),cost:Number((parts[1]||'').trim())||0}}).filter(x=>x.item);const spentGold=purchases.reduce((n,x)=>n+x.cost,0);if(spentGold>(Number($('startingGold').value)||0)){notify(`Equipment costs ${spentGold} gp, exceeding starting gold by ${spentGold-(Number($('startingGold').value)||0)} gp.`,true);return}const data={schemaVersion:1,diceSet:$('diceSet').value,startingGoldRoll:$('goldFormula').textContent,purchases,remainingGold:(Number($('startingGold').value)||0)-spentGold,concept:$('concept').value,background:$('background').value,abilityRolls:rollData,startingGoldResult:goldRollData,abilityAssignment:{strength:$('str').value,dexterity:$('dex').value,constitution:$('con').value,intelligence:$('int').value,wisdom:$('wis').value,charisma:$('cha').value},skills:skillValues,skillMisc,saveMisc,feats:$('feats').value.split('\n').map(x=>x.trim()).filter(Boolean),prestigeGoal:$('prestigeGoal').value,prestigeNotes:$('prestigeNotes').value,startingGold:Number($('startingGold').value)||0,equipment:$('equipment').value,gearNotes:$('gearNotes').value,skillPointsAvailable:total,skillPointsSpent:spent};const body={name:$('charName').value.trim(),playerName:$('playerName').value.trim()||null,campaignId:$('campaignId').value||null,race:$('race').value,className:$('className').value,alignment:$('alignment').value,deity:$('deity').value||null,characterLevel:1,creationStatus:'Created',abilityScores:scores,data};try{const saved=await api(editingId?`${API}/${editingId}`:API,{method:editingId?'PUT':'POST',body:JSON.stringify(body)});editingId=saved.id;draftActive=false;notify(`Saved ${saved.name}. Character record is in Campaign Forge.`);await load();renderList()}catch(err){notify(`Save failed: ${err.message}`,true)}});
let touchPending=false;
async function touchDraft(){if(!draftActive||!editingId||touchPending||Date.now()-lastDraftTouch<300000)return;touchPending=true;try{await api(`${API}/${editingId}/activity`,{method:'POST'});lastDraftTouch=Date.now()}catch{}finally{touchPending=false}}
document.addEventListener('input',touchDraft,true);document.addEventListener('click',touchDraft,true);
fillAbilitySelects();resetForm();load();
