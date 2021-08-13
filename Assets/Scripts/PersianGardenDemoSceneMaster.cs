﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

public class PersianGardenDemoSceneMaster : MonoBehaviour
{

    // ------------------------------------------------------------------
    // VARIABLES

    //___________
    // public
    public FluidSimulater fluid_simulater;

    [Header("Fountain Managment")]
    public Transform  Tran_downLeft;
    public Transform  Tran_upRight;
    public Transform  mainDirectionLight;
    public GameObject Fountain;
    public GameObject FountainPlain;
    public Texture2D  boundaryTexture;

    public bool       debug_ink;

    //___________
    // private
    private FluidGPUResources resources;
    private Material          fountainMaterial;

    private RenderTexture     pressure_texture;

    private Camera            main_cam;
    private Camera            reflection_cam;
    private RenderTexture     reflection_cam_texture;
    // ------------------------------------------------------------------
    // INITALISATION
    void Start()
    {

        main_cam = Camera.main;
        if (main_cam == null) Debug.LogError("Could not find main camera, make sure the camera is tagged as main");


        // Set up the refelection camera
        GameObject refCamGB = new GameObject("RefelectionCamera");
        reflection_cam =  refCamGB.AddComponent<Camera>();
        reflection_cam.CopyFrom(main_cam); // This needs to sync if cam changes in run time

        reflection_cam_texture = new RenderTexture(main_cam.pixelWidth, main_cam.pixelHeight, 8);
        reflection_cam_texture.Create();

        reflection_cam.targetTexture = reflection_cam_texture;

        Shader.SetGlobalTexture("_Refelection_texture", reflection_cam_texture);
        Shader.SetGlobalVector("_refCamScreenParm", new Vector2(main_cam.pixelWidth, main_cam.pixelHeight));

        //--

        fluid_simulater.Initialize();
        resources = new FluidGPUResources(fluid_simulater);
        resources.Create();

        fluid_simulater.SubmitMousePosOverrideDelegate(GetMousePosInSimulationSpaceUnitValue);

        pressure_texture = new RenderTexture((int)fluid_simulater.canvas_dimension, (int)fluid_simulater.canvas_dimension, 0)
        {
            enableRandomWrite = true,
            useMipMap         = true,
             graphicsFormat = UnityEngine.Experimental.Rendering.GraphicsFormat.R32_SFloat,
            filterMode        = FilterMode.Trilinear,
            anisoLevel        = 7,
            format            = RenderTextureFormat.RFloat,
            wrapMode          = TextureWrapMode.Clamp,
        };

        pressure_texture.Create();

        //--

        Shader.SetGlobalVector("_canvas_texel_size", new Vector4(1.0f/fluid_simulater.canvas_dimension, 1.0f / fluid_simulater.canvas_dimension, 0.0f, 0.0f));
        Shader.SetGlobalVector("_lightDirection"   , mainDirectionLight.forward);
        Shader.SetGlobalMatrix("_fountain2World"   , Tran_downLeft.localToWorldMatrix);

        //--

        fluid_simulater.UpdateArbitaryBoundaryOffsets(boundaryTexture, resources);


        fluid_simulater.AddUserForce           (resources.velocity_buffer                                   );
        fluid_simulater.HandleCornerBoundaries (resources.velocity_buffer, FieldType.Velocity               );
        fluid_simulater.HandleCornerBoundaries (resources.pressure_buffer, FieldType.Pressure               );
        fluid_simulater.HandleArbitaryBoundary(resources.velocity_buffer, resources.boundary_velocity_offset_buffer, FieldType.Velocity);
        fluid_simulater.HandleArbitaryBoundary(resources.pressure_buffer, resources.boundary_pressure_offset_buffer, FieldType.Pressure);
        fluid_simulater.Diffuse                (resources.velocity_buffer                                   );
        fluid_simulater.HandleCornerBoundaries (resources.velocity_buffer, FieldType.Velocity               );
        fluid_simulater.HandleCornerBoundaries (resources.pressure_buffer, FieldType.Pressure               );
        fluid_simulater.HandleArbitaryBoundary(resources.velocity_buffer, resources.boundary_velocity_offset_buffer, FieldType.Velocity);
        fluid_simulater.HandleArbitaryBoundary(resources.pressure_buffer, resources.boundary_pressure_offset_buffer, FieldType.Pressure);
        fluid_simulater.Project                (resources.velocity_buffer, resources.divergence_buffer, resources.pressure_buffer);
        fluid_simulater.Advect                 (resources.velocity_buffer, resources.velocity_buffer, fluid_simulater.velocity_dissapation);
        fluid_simulater.HandleCornerBoundaries (resources.velocity_buffer, FieldType.Velocity               );
        fluid_simulater.HandleCornerBoundaries (resources.pressure_buffer, FieldType.Pressure               );
        fluid_simulater.HandleArbitaryBoundary(resources.velocity_buffer, resources.boundary_velocity_offset_buffer, FieldType.Velocity);
        fluid_simulater.HandleArbitaryBoundary(resources.pressure_buffer, resources.boundary_pressure_offset_buffer, FieldType.Pressure);
        fluid_simulater.Project                (resources.velocity_buffer, resources.divergence_buffer, resources.pressure_buffer);
        

        //fluid_simulater.AddDye                 (resources.dye_buffer                                        );
        //fluid_simulater.Advect                 (resources.dye_buffer, resources.velocity_buffer, 0.9999f    );
        //fluid_simulater.HandleCornerBoundaries (resources.dye_buffer, FieldType.Dye                         );
        //fluid_simulater.Diffuse                (resources.dye_buffer                                        );
        //fluid_simulater.HandleCornerBoundaries (resources.dye_buffer, FieldType.Dye                         );


        fluid_simulater.CopyBufferToTexture(pressure_texture, resources.pressure_buffer);

        Shader.SetGlobalTexture("_fountain_pressure_buffer", pressure_texture);

        


        fluid_simulater.BindCommandBuffer();

    }

    // ------------------------------------------------------------------
    // DESTRUCTOR
    void OnDisable()
    {
        fluid_simulater.Release();
        resources      .Release();
    }

    // ------------------------------------------------------------------
    // LOOP
    void Update()
    {
        fluid_simulater.Tick(Time.deltaTime);


        Shader.SetGlobalVector("_fountain_downLeft", Tran_downLeft.position);
        Shader.SetGlobalVector("_fountain_upRight", Tran_upRight.position  );

        // Update ref cam



        // Refelecting the camera

        Vector4 mainCamPos = FountainPlain.transform.InverseTransformPoint(main_cam.transform.position);
        Vector4 refCamPos  = new Vector4(mainCamPos.x, -1.0f * mainCamPos.y, mainCamPos.z, 1.0f);
                refCamPos  = FountainPlain.transform.TransformPoint(refCamPos);
        reflection_cam.transform.position = refCamPos;

        Vector4 mainCamForward = FountainPlain.transform.InverseTransformDirection(main_cam.transform.forward);
        Vector4 refCamForward = new Vector4(mainCamForward.x, -mainCamForward.y, mainCamForward.z, 0.0f); 
                refCamForward = FountainPlain.transform.TransformDirection(refCamForward);
      
        Vector4 mainCamUp = FountainPlain.transform.InverseTransformDirection(main_cam.transform.up);
        Vector4 refCamUp  = new Vector4(mainCamUp.x, -mainCamUp.y, mainCamUp.z, 0.0f);
                refCamUp  = FountainPlain.transform.TransformDirection(refCamUp);

        reflection_cam.transform.LookAt(refCamPos + refCamForward, refCamUp);

        Vector3 waterPlanePosInCameraSpace    = reflection_cam.worldToCameraMatrix.MultiplyPoint (FountainPlain.transform.position);
        Vector3 waterPlaneNormalInCameraSpace = reflection_cam.worldToCameraMatrix.MultiplyVector(FountainPlain.transform.up);
        float   DstInCameraSpace = -Vector3.Dot(waterPlanePosInCameraSpace, waterPlaneNormalInCameraSpace) ;

        Vector4 clipPlaneCameraSpace = new Vector4(waterPlaneNormalInCameraSpace.x, waterPlaneNormalInCameraSpace.y, waterPlaneNormalInCameraSpace.z, DstInCameraSpace);
        Matrix4x4 newMat = main_cam.CalculateObliqueMatrix(clipPlaneCameraSpace);

        reflection_cam.projectionMatrix = newMat;


        Shader.SetGlobalMatrix("_ref_cam_tranform", GL.GetGPUProjectionMatrix(reflection_cam.projectionMatrix, true) * reflection_cam.worldToCameraMatrix);

    }

    // ------------------------------------------------------------------
    // FUNCTIONS

    Vector2 GetMousePosInSimulationSpaceUnitValue(ref bool isInBound)
    {
        RaycastHit results;
        Ray ray = main_cam.ScreenPointToRay(Input.mousePosition);

        isInBound = false;
        if (Physics.Raycast(ray, out results, 100.0f))
        {

            if (results.collider.gameObject != Fountain) return new Vector2(-20.0f, -20.0f);

            Vector2 hitPositionInSimulationSpace = new Vector2(results.point.z, results.point.x);
                    hitPositionInSimulationSpace = hitPositionInSimulationSpace - new Vector2(Tran_downLeft.position.z, Tran_downLeft.position.x);

            Vector2 span = new Vector2(Tran_upRight.position.z, Tran_upRight.position.x) - new Vector2(Tran_downLeft.position.z, Tran_downLeft.position.x);

             hitPositionInSimulationSpace = new Vector2(hitPositionInSimulationSpace.x / Mathf.Abs(span.x), hitPositionInSimulationSpace.y / Mathf.Abs(span.y));
            isInBound = true;
            return hitPositionInSimulationSpace;

        }

        return new Vector2(-20.0f, -20.0f);
    }
}
