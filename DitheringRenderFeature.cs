using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.RenderGraphModule.Util;

public class DitheringRenderFeature : ScriptableRendererFeature
{
    [SerializeField] DitheringRenderFeatureSettings settings;
    DitheringRenderFeaturePass m_ScriptablePass;


    // Use this class to pass around settings from the feature to the pass
    [Serializable]
    public class DitheringRenderFeatureSettings
    {
        
    }

    class DitheringRenderFeaturePass : ScriptableRenderPass
    {
		const string m_PassName="DitherEfectPass";
		Material m_BlitMaterial;
		
        readonly DitheringRenderFeatureSettings settings;

        public DitheringRenderFeaturePass(DitheringRenderFeatureSettings settings)
        {
            this.settings = settings;
        }

       public void Setup(Material mat)
	   {
		   m_BlitMaterial = mat;
		   requiresIntermediateTexture = true;//forces Unity make a working copy of the output to be passed in for the effect
	   }

        

        // RecordRenderGraph is where the RenderGraph handle can be accessed, through which render passes can be added to the graph.
        // FrameData is a context container through which URP resources can be accessed and managed.
		
		//change camera color buffer to temp texture while applying our mat
        public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
        {
			//dithering only to be working within volume
			var stack = VolumeManager.instance.stack;
			var customEffect = stack.GetComponent<SphereVolumeComponent>();
			
			//if the volume is not active then nothing happens
			if(!customEffect.IsActive())
				return;
            var resourceData = frameData.Get<UniversalResourceData>();//access point to all renderers texture handles
			if(resourceData.isActiveTargetBackBuffer)
			{
				Debug.LogError($"Skipping render pass. ditherRenderFeature requires intermediate ColorTexture. Can't use BackBuffer as texture input.");
				return;
			}
			
			var source = resourceData.activeColorTexture; //takes active colorTexture in our resourceData
			var destinationDesc = renderGraph.GetTextureDesc(source);//descriptor struct where render texture properties are stored
			destinationDesc.name=$"CameraColor-{m_PassName}";
			destinationDesc.clearBuffer = false; //no blank state, we modify current.
			
			TextureHandle destination = renderGraph.CreateTexture(destinationDesc);
			
			RenderGraphUtils.BlitMaterialParameters para = new(source, destination, m_BlitMaterial, 0);
			renderGraph.AddBlitPass(para, passName: m_PassName);//executes blit using our material
			
			resourceData.cameraColor= destination; //swap camera color buffer with our texture
        }
    }
	public RenderPassEvent injectionPoint = RenderPassEvent.AfterRenderingPostProcessing;
	public Material material;
	
	
	//DitherEffectPass m_ScriptablePass
	/// <inheritdoc/>
    public override void Create()
    {
        m_ScriptablePass = new DitheringRenderFeaturePass(settings);

        // Configures where the render pass should be injected.
        m_ScriptablePass.renderPassEvent = injectionPoint;

        // You can request URP color texture and depth buffer as inputs by uncommenting the line below,
        // URP will ensure copies of these resources are available for sampling before executing the render pass.
        // Only uncomment it if necessary, it will have a performance impact, especially on mobiles and other TBDR GPUs where it will break render passes.
        //m_ScriptablePass.ConfigureInput(ScriptableRenderPassInput.Color | ScriptableRenderPassInput.Depth);

        // You can request URP to render to an intermediate texture by uncommenting the line below.
        // Use this option for passes that do not support rendering directly to the backbuffer.
        // Only uncomment it if necessary, it will have a performance impact, especially on mobiles and other TBDR GPUs where it will break render passes.
        //m_ScriptablePass.requiresIntermediateTexture = true;
    }
	
	// Here you can inject one or multiple render passes in the renderer.
    // This method is called when setting up the renderer once per-camera.
    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
		if(material == null)
		{
			Debug.LogWarning("DitheringRenderEffect material is null. Will be skipped");
			return;
		}
		//call setup method for the pass with material as parameter
		m_ScriptablePass.Setup(material);
        renderer.EnqueuePass(m_ScriptablePass);
    }
}
