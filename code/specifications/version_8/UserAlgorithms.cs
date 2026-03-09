using UnityEngine;
using System.Collections.Generic;

namespace Version_8
{
    public static class UserAlgorithms
    {
        // Tracks which vinyl is currently on the platter (null = none)
        private static GameObject currentVinyl = null;

        // ================================================================
        //  LOCAL STATE MANAGEMENT
        //  (Standalone dictionary — works before VReqDV generation.
        //   After "Display Mock-up" generates StateAccessor, you can
        //   swap these calls to VReqDV.StateAccessor if desired.)
        // ================================================================
        private static Dictionary<string, string> objectStates = new Dictionary<string, string>();

        private static void InitDefaultState(string objectName, string defaultState)
        {
            if (!objectStates.ContainsKey(objectName))
                objectStates[objectName] = defaultState;
        }

        public static void SetState(string objectName, string stateName)
        {
            objectStates[objectName] = stateName;
        }

        public static bool IsState(string objectName, string stateName)
        {
            InitDefaults();
            if (objectStates.ContainsKey(objectName))
                return objectStates[objectName] == stateName;
            return false;
        }

        private static bool defaultsInitialized = false;
        private static void InitDefaults()
        {
            if (defaultsInitialized) return;
            defaultsInitialized = true;
            // First state in article.json list is the default
            InitDefaultState("Vinyl_Record_A", "idle");
            InitDefaultState("Vinyl_Record_B", "idle");
            InitDefaultState("Turntable_Platter", "empty");
            InitDefaultState("Master_Button", "off");
        }

        // ================================================================
        //  CONDITION METHODS (return bool)
        // ================================================================

        /// <summary>
        /// Checks whether the given vinyl object is close enough to the platter
        /// to be considered a "collision" / placement attempt.
        /// </summary>
        public static bool IsCollidingWithPlatter(GameObject obj)
        {
            GameObject platter = GameObject.Find("Turntable_Platter");
            if (platter == null) return false;

            float dist = Vector3.Distance(obj.transform.position, platter.transform.position);
            // Platter radius is ~0.2 (scale 0.4). Use 0.25 threshold.
            return dist < 0.25f;
        }

        /// <summary>
        /// Checks if the user clicked on the given object this frame (mouse raycast).
        /// </summary>
        public static bool IsButtonClicked(GameObject obj)
        {
            if (Input.GetMouseButtonDown(0))
            {
                Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
                RaycastHit hit;
                if (Physics.Raycast(ray, out hit))
                {
                    return hit.collider.gameObject == obj;
                }
            }
            return false;
        }

        /// <summary>
        /// Returns true if a vinyl was loaded but has moved far enough
        /// from the platter to count as "removed".
        /// </summary>
        public static bool IsVinylRemoved(GameObject obj)
        {
            if (currentVinyl == null) return false;

            float dist = Vector3.Distance(obj.transform.position, currentVinyl.transform.position);
            return dist > 0.25f;
        }

        // ================================================================
        //  ACTION METHODS (return void)
        // ================================================================

        /// <summary>
        /// Snaps a vinyl record onto the turntable platter:
        ///  - Moves vinyl to platter center (slightly above)
        ///  - Freezes its physics (kinematic)
        ///  - Transitions states: vinyl -> on_platter, platter -> loaded
        /// </summary>
        public static void SnapToPlatter(GameObject obj)
        {
            GameObject platter = GameObject.Find("Turntable_Platter");
            if (platter == null) return;

            // Position vinyl on top of the platter
            Vector3 snapPos = platter.transform.position + new Vector3(0f, 0.02f, 0f);
            obj.transform.position = snapPos;
            obj.transform.rotation = Quaternion.identity;

            // Freeze physics so the vinyl stays in place
            Rigidbody rb = obj.GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.velocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
                rb.isKinematic = true;
            }

            // Track the placed vinyl
            currentVinyl = obj;

            // State transitions
            SetState(obj.name, "on_platter");
            SetState("Turntable_Platter", "loaded");
        }

        /// <summary>
        /// Toggles the platter between "playing" and "loaded" states,
        /// and flips the Master_Button between "on" and "off".
        /// </summary>
        public static void TogglePlayback(GameObject obj)
        {
            GameObject platter = GameObject.Find("Turntable_Platter");
            if (platter == null) return;

            if (IsState("Turntable_Platter", "playing"))
            {
                // Currently playing -> pause (go back to loaded)
                SetState("Turntable_Platter", "loaded");
                SetState("Master_Button", "off");

                // Visual feedback: button turns red
                SetButtonColor(obj, Color.red);
            }
            else
            {
                // Currently loaded -> start playing
                SetState("Turntable_Platter", "playing");
                SetState("Master_Button", "on");

                // Visual feedback: button turns green
                SetButtonColor(obj, Color.green);
            }
        }

        /// <summary>
        /// Spins the platter (and the vinyl on it) each frame while playing.
        /// Called every Update frame when platter state == "playing".
        /// </summary>
        public static void RotatePlatter(GameObject obj)
        {
            float spinSpeed = 90f; // degrees per second
            obj.transform.Rotate(0f, spinSpeed * Time.deltaTime, 0f);

            // Also spin the vinyl sitting on the platter
            if (currentVinyl != null)
            {
                currentVinyl.transform.Rotate(0f, spinSpeed * Time.deltaTime, 0f);
            }
        }

        /// <summary>
        /// Handles the case where the vinyl has been dragged away from the platter:
        ///  - Resets the vinyl's state to "idle"
        ///  - Resets the platter to "empty"
        ///  - Turns the button off
        /// </summary>
        public static void HandleVinylRemoval(GameObject obj)
        {
            if (currentVinyl != null)
            {
                // Restore vinyl physics so it can be picked up again
                Rigidbody rb = currentVinyl.GetComponent<Rigidbody>();
                if (rb != null)
                {
                    rb.isKinematic = false;
                    rb.useGravity = true;
                }

                SetState(currentVinyl.name, "idle");
            }

            currentVinyl = null;

            // Reset platter and button
            SetState("Turntable_Platter", "empty");

            GameObject button = GameObject.Find("Master_Button");
            if (button != null)
            {
                SetState("Master_Button", "off");
                SetButtonColor(button, Color.red);
            }
        }

        /// <summary>
        /// Pushes a vinyl away from the platter when the platter already
        /// has a record loaded. Resets the vinyl to its starting position.
        /// </summary>
        public static void RejectVinyl(GameObject obj)
        {
            // Push the vinyl back to a safe position away from the platter
            Rigidbody rb = obj.GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.velocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
                rb.isKinematic = false;
                rb.useGravity = true;

                // Apply a small impulse to push it away
                Vector3 awayDir = (obj.transform.position - GameObject.Find("Turntable_Platter").transform.position).normalized;
                if (awayDir.magnitude < 0.01f) awayDir = Vector3.right;
                rb.AddForce(awayDir * 1f, ForceMode.Impulse);
            }
        }

        // ================================================================
        //  HELPER METHODS (private)
        // ================================================================

        /// <summary>
        /// Changes the material color of the Master_Button for visual feedback.
        /// </summary>
        private static void SetButtonColor(GameObject button, Color color)
        {
            if (button == null) return;

            Renderer r = button.GetComponent<Renderer>();
            if (r != null && Application.isPlaying)
            {
                r.material.color = color;
            }
        }
    }
}
