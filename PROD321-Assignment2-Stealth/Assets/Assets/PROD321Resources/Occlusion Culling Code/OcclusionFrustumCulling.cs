﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/* This class implements occlusion frustum culling
 * 
 * PROD321 - Interactive Computer Graphics and Animation 
 * Copyright 2021, University of Canterbury
 * Written by Adrian Clark
 */

public class OcclusionFrustumCulling : MonoBehaviour
{
    // The camera we will be generating occlusion frustums from
    public Camera occlusionCamera;

    // An array of mesh filters of objects which can occlude other objects
    public MeshFilter[] occlusionObjects;

    // A list of the occlusion frustums we will create for the objects above
    List<OcclusionFrustum> occlusionFrustums = new List<OcclusionFrustum>();

    // The material to use for our frustums
    public Material frustumMaterial;

    // The layer mask for the frustums
    public string frustumLayerName = "Frustums";

    // An array of game objects to test for occulusion
    public Renderer[] gameObjectsToTestForOcclusion;

    // The game objects which are not occluded in the current frame
    public List<GameObject> gameObjectsNotOccluded = new List<GameObject>();

    // An array of visibility spheres for these game objects
    SphereCollider[] visibilitySpheres;

    // The material to use for our visibility spheres
    public Material visibilitySphereMaterial;

    // Start is called before the first frame update
    void Start()
    {
        // If the occlusion camera hasn't been defined, try get the
        // camera tagged with MainCamera
        if (occlusionCamera == null)
            occlusionCamera = Camera.main;

        // Instantiate the visibility spheres list based on the number of game
        // objects to test for visibility
        visibilitySpheres = new SphereCollider[gameObjectsToTestForOcclusion.Length];

        // Loop through each game object to test for visibility
        for (int i = 0; i < gameObjectsToTestForOcclusion.Length; i++)
        {
            // If the game object already has a visibility sphere
            if (gameObjectsToTestForOcclusion[i].transform.Find("VisibilitySphere") != null)
            {
                // Lets just get use that one instead
                GameObject visibilitySphere = gameObjectsToTestForOcclusion[i].transform.Find("VisibilitySphere").gameObject;
                // Store the collider for the sphere into the visibility spheres array
                visibilitySpheres[i] = visibilitySphere.GetComponent<SphereCollider>();
            }
            else
            {
                // Create a new sphere
                GameObject visibilitySphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                // Set the gameobjects name to "VisibilitySphere"
                visibilitySphere.name = "VisibilitySphere";
                // Set it's parent as the game object
                visibilitySphere.transform.SetParent(gameObjectsToTestForOcclusion[i].transform, false);
                // Set it's size to the the game objects bounding box's extent magnitude * 2
                // (the magnitude is the half the size)
                visibilitySphere.transform.localScale = Vector3.one * gameObjectsToTestForOcclusion[i].localBounds.extents.magnitude * 2;
                // Get the sphere's Renderer and update the material to our visibility sphere material, and turn off shadows
                Renderer renderer = visibilitySphere.GetComponent<Renderer>();
                renderer.material = visibilitySphereMaterial;
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                renderer.receiveShadows = false;
                // Store the collider for the sphere into the visibility spheres array
                visibilitySpheres[i] = visibilitySphere.GetComponent<SphereCollider>();
            }
        }

        // Loop through all our occlusion objects
        foreach (MeshFilter occlusionObject in occlusionObjects)
        {
            if (occlusionObject != null)
            {
                // Get the Occlusion Frustum on this object
                OcclusionFrustum occlusionFrustum = occlusionObject.gameObject.GetComponent<OcclusionFrustum>();
                // If there is none, add an occlusion frustum to it
                if (occlusionFrustum == null) occlusionFrustum = occlusionObject.gameObject.AddComponent<OcclusionFrustum>();
                // Add this occlusion frustum to our list of occlusion frustums
                occlusionFrustums.Add(occlusionFrustum);
            }
        }
    }

    // Update is called once per frame
    void Update()
    {
        // Clear the list of game objects in the frustum
        gameObjectsNotOccluded.Clear();

        // Create a list of visibile objects, and populate it will all objects
        // to start with (we will remove objects as we determine they are not
        // visible
        List<SphereCollider> visibleObjects = new List<SphereCollider>(visibilitySpheres);

        // Loop through each occlusion frustum
        foreach (OcclusionFrustum occlusionFrustum in occlusionFrustums)
        {
            // Calculate the frustum corners
            Vector3[] frustumCorners = CalculateFrustumCorners(occlusionCamera);


            //Check the occlusionFrustums, and if they are in our camera.


            // Draw debug lines for the frustum bounds

            for (int i = 0; i < frustumCorners.Length; i++)
            {
                Debug.DrawLine(frustumCorners[i], frustumCorners[(i + 1) % frustumCorners.Length], Color.white);
            }

            // Create lists for the frustum's plane's centers and normals
            List<Vector3> transformedFrustumCenters = new List<Vector3>();
            List<Vector3> transformedFrustumNormals = new List<Vector3>();

            // Get the frustum plane's centers and normals and store them in our lists
            occlusionFrustum.CalcFrustum(occlusionCamera, ref transformedFrustumCenters, ref transformedFrustumNormals);

            float normalLength = 5;
            // Draw the normals for our occlusion frustum planes
            for (int j = 0; j < transformedFrustumCenters.Count; j++) 
                Debug.DrawRay(transformedFrustumCenters[j], transformedFrustumNormals[j] * normalLength, Color.white);

            // Loop through all our visible objects
            for (int i = 0; i < visibleObjects.Count; i++)
            {
                // Assume it's in the frustum by default
                bool inFrustum = true;

                // Get the position of this objects bounding sphere
                Vector3 spherePos = visibleObjects[i].transform.position;


                // Calculate the radius of the object's bounding sphere
                float bounds = visibleObjects[i].radius * visibleObjects[i].transform.localScale.x;

                // Loop through all our transformed frustum planes
                for (int j = 0; j< transformedFrustumCenters.Count; j++)
                {
                    // Calculate the h distance of the point relative to each of the frustum planes
                    // the formula for h distance is h = (P - P_0) . N
                    // Where P is the point we are wanting to check what side of the plane it lays on
                    // P_0 is a point on the plane we're testing, and N is the planes normal
                    float h = Vector3.Dot((spherePos - transformedFrustumCenters[j]), transformedFrustumNormals[j]);

                    // If the h value is greater than the outer bounds limit
                    if (h>-bounds)
                    {
                        // The object is at least *partly* out of the occlusion volume
                        // set "inFrustum" to false and break
                        inFrustum = false;
                        break;
                    }
                }

                // Otherwise, if it was within all the bounds
                if (inFrustum)
                {
                    // It's completely in the frustum, remove it from
                    // the list of visible objects
                    visibleObjects.RemoveAt(i);

                    // Reduce i because we have removed this object from the list
                    i--;
                }
            }
        }

        // Loop through each game object we're testing
        for (int i = 0; i < gameObjectsToTestForOcclusion.Length; i++)
        {
            int frustumsLayerMask = 1 << LayerMask.NameToLayer("Frustums");
            int layerMask = ~frustumsLayerMask;

            // Get the game object and its visibility sphere
            GameObject GO = gameObjectsToTestForOcclusion[i].gameObject;
            SphereCollider sphereCollider = visibilitySpheres[i].GetComponent<SphereCollider>();

            // Perform occlusion test
            // Get the object's bounds
            Bounds bounds = sphereCollider.bounds;

            // Check if the object's bounds are within the camera's view frustum
            if (GeometryUtility.TestPlanesAABB(GeometryUtility.CalculateFrustumPlanes(occlusionCamera), bounds))
            {
                // Perform a raycast from the camera towards the object's position
                Vector3 direction = GO.transform.position - occlusionCamera.transform.position;
                RaycastHit hit;
                Debug.DrawLine(occlusionCamera.transform.position, GO.transform.position, Color.red);
                Debug.Log("Character in bounds of camera.");

                if (Physics.Raycast(occlusionCamera.transform.position, direction, out hit, Mathf.Infinity))
                {
                    Debug.Log("We hit object: " + hit.collider.gameObject.name);
                    if (hit.collider.gameObject.GetComponent<SphereCollider>() != null || hit.collider.gameObject == GO || hit.collider.gameObject == sphereCollider.gameObject)
                    {
                        Debug.Log("Added to list");
                        gameObjectsNotOccluded.Add(GO);
                    }
                }
            }
        }
    }

    // Function to calculate the frustum corners
    Vector3[] CalculateFrustumCorners(Camera camera)
    {
        Vector3[] frustumCorners = new Vector3[8];

        float nearDist = camera.nearClipPlane;
        float farDist = camera.farClipPlane;
        float aspect = camera.aspect;
        float fov = camera.fieldOfView * 0.5f;
        float heightNear = 2.0f * Mathf.Tan(fov) * nearDist;
        float widthNear = heightNear * aspect;
        float heightFar = 2.0f * Mathf.Tan(fov) * farDist;
        float widthFar = heightFar * aspect;

        Transform cameraTransform = camera.transform;
        Vector3 cameraPos = cameraTransform.position;
        Vector3 cameraForward = cameraTransform.forward;
        Vector3 cameraRight = cameraTransform.right;
        Vector3 cameraUp = cameraTransform.up;

        frustumCorners[0] = cameraPos + cameraForward * nearDist - cameraRight * widthNear * 0.5f - cameraUp * heightNear * 0.5f; // Bottom left near
        frustumCorners[1] = cameraPos + cameraForward * nearDist + cameraRight * widthNear * 0.5f - cameraUp * heightNear * 0.5f; // Bottom right near
        frustumCorners[2] = cameraPos + cameraForward * nearDist + cameraRight * widthNear * 0.5f + cameraUp * heightNear * 0.5f; // Top right near
        frustumCorners[3] = cameraPos + cameraForward * nearDist - cameraRight * widthNear * 0.5f + cameraUp * heightNear * 0.5f; // Top left near
        frustumCorners[4] = cameraPos + cameraForward * farDist - cameraRight * widthFar * 0.5f - cameraUp * heightFar * 0.5f; // Bottom left far
        frustumCorners[5] = cameraPos + cameraForward * farDist + cameraRight * widthFar * 0.5f - cameraUp * heightFar * 0.5f; // Bottom right far
        frustumCorners[6] = cameraPos + cameraForward * farDist + cameraRight * widthFar * 0.5f + cameraUp * heightFar * 0.5f; // Top right far
        frustumCorners[7] = cameraPos + cameraForward * farDist - cameraRight * widthFar * 0.5f + cameraUp * heightFar * 0.5f; // Top left far

        return frustumCorners;
    }
}
