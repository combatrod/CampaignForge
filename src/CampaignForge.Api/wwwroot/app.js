const api="/api/campaigns", grid=document.getElementById("campaignGrid");
const dialog=document.getElementById("campaignDialog"), form=document.getElementById("campaignForm");
const message=document.getElementById("message"); let campaigns=[];
const el=id=>document.getElementById(id);
const esc=v=>String(v??"").replace(/[&<>"']/g,c=>({"&":"&amp;","<":"&lt;",">":"&gt;",'"':"&quot;","'":"&#39;"}[c]));
function msg(t="",bad=false){message.textContent=t;message.className=bad?"message error":"message"}
async function call(url,options={}){const r=await fetch(url,{...options,headers:{"Content-Type":"application/json",...(options.headers||{})}});if(!r.ok){let m=`${r.status} ${r.statusText}`;try{const b=await r.json();m=b.title||b.detail||m}catch{}throw Error(m)}return r.status===204?null:r.json()}
function render(){
 const q=el("searchInput").value.trim().toLowerCase(),s=el("statusFilter").value;
 const rows=campaigns.filter(c=>(!s||c.status===s)&&(!q||`${c.name} ${c.setting||""} ${c.description||""}`.toLowerCase().includes(q)));
 el("campaignCount").textContent=campaigns.length;
 grid.innerHTML=rows.map(c=>`<article class="campaign-card"><div class="card-top"><span class="status ${c.status.toLowerCase()}">${esc(c.status)}</span></div>
 <div class="card-setting">${esc(c.setting||"Uncharted setting")}</div><h2>${esc(c.name)}</h2><p class="card-description">${esc(c.description||"No campaign description yet.")}</p>
 <div class="card-footer"><span>UPDATED ${new Date(c.updatedUtc).toLocaleDateString()}</span><span class="card-actions">
 <button class="text-button" data-edit="${c.id}">Edit</button><button class="text-button delete" data-delete="${c.id}">Delete</button></span></div></article>`).join("");
 el("emptyState").hidden=rows.length!==0;
}
async function load(){try{msg("Loading campaigns…");campaigns=await call(api);msg();render()}catch(e){msg(`Unable to load campaigns: ${e.message}`,true)}}
function newCampaign(){form.reset();el("campaignId").value="";el("dialogTitle").textContent="New campaign";el("status").value="Planning";dialog.showModal();el("name").focus()}
function editCampaign(c){el("campaignId").value=c.id;el("name").value=c.name;el("setting").value=c.setting||"";el("description").value=c.description||"";el("status").value=c.status;el("dialogTitle").textContent="Edit campaign";dialog.showModal();el("name").focus()}
el("newCampaignButton").addEventListener("click",newCampaign);el("emptyCreateButton").addEventListener("click",newCampaign);
el("closeDialog").addEventListener("click",()=>dialog.close());el("cancelDialog").addEventListener("click",()=>dialog.close());
el("searchInput").addEventListener("input",render);el("statusFilter").addEventListener("change",render);
grid.addEventListener("click",async e=>{
 if(e.target.dataset.edit){const c=campaigns.find(x=>x.id===e.target.dataset.edit);if(c)editCampaign(c)}
 if(e.target.dataset.delete){const c=campaigns.find(x=>x.id===e.target.dataset.delete);if(!c||!confirm(`Delete "${c.name}"? This cannot be undone.`))return;try{await call(`${api}/${c.id}`,{method:"DELETE"});msg(`Deleted "${c.name}".`);await load()}catch(err){msg(`Delete failed: ${err.message}`,true)}}
});
form.addEventListener("submit",async e=>{
 e.preventDefault();const id=el("campaignId").value;
 const body={name:el("name").value.trim(),setting:el("setting").value.trim()||null,description:el("description").value.trim()||null,status:el("status").value};
 if(!body.name)return;const b=el("saveButton");b.disabled=true;b.textContent="Saving…";
 try{await call(id?`${api}/${id}`:api,{method:id?"PUT":"POST",body:JSON.stringify(body)});dialog.close();msg(id?"Campaign updated.":"Campaign created.");await load()}
 catch(err){msg(`Save failed: ${err.message}`,true)}finally{b.disabled=false;b.textContent="Save campaign"}
});
load();
