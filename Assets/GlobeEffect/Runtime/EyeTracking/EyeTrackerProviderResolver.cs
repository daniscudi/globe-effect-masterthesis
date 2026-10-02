using System;
using System.Collections.Generic;
using System.Diagnostics;
using UnityEngine;
using UnityEngine.XR;
using UnityEngine.XR.Management;
using UnityEngine.XR.OpenXR;
using Varjo.XR;
using Debug = UnityEngine.Debug;
using ETProvider = GlobeEffect.VRCheckerboard.EyeTracking.EyeTrackingToolbox.ETProvider;

namespace GlobeEffect.VRCheckerboard.EyeTracking
{
    /// <summary>
    /// Löst die Einstellung "Auto" der EyeTrackingToolbox zur Laufzeit auf und
    /// schützt Messungen davor, unbemerkt mit dem Dummy zu laufen. Die Logik liegt
    /// bewusst außerhalb der Lab-Toolbox; die Toolbox ruft sie nur auf.
    ///
    /// Reihenfolge bei Auto:
    /// 1. Varjo, wenn die XR-Sitzung über den Varjo-Loader läuft.
    /// 2. SRanipal, wenn die SRanipal-Runtime (sr_runtime.exe) läuft.
    /// 3. Sonst Dummy (Blick folgt der Maus) für den Test am Laptop.
    /// </summary>
    public static class EyeTrackerProviderResolver
    {
        // Prozessname der SRanipal-Runtime, wie ihn der Task-Manager ohne ".exe"
        // zeigt. Sie wird mit der VIVE Console / SRanipal-Installation gestartet.
        public const string SRanipalRuntimeProcessName = "sr_runtime";

        private const string NoLoaderName = "keiner";

        private static readonly List<XRDisplaySubsystem> displaySubsystems = new();

        /// <summary>
        /// Die eigentliche Entscheidung, ohne Zugriff auf Hardware oder Unity.
        /// Ausdrücklich eingestellte Provider werden nie überstimmt.
        /// </summary>
        public static ETProvider Resolve(
            ETProvider configured,
            bool varjoLoaderActive,
            bool sranipalRuntimeRunning)
        {
            if (configured != ETProvider.Auto)
            {
                return configured;
            }

            if (varjoLoaderActive)
            {
                return ETProvider.Varjo;
            }

            return sranipalRuntimeRunning ? ETProvider.SRanipal : ETProvider.Dummy;
        }

        /// <summary>
        /// Wird von der Toolbox beim Start aufgerufen. Fragt den aktuellen Rechner
        /// ab, entscheidet und schreibt XR-Loader und Eye-Tracker ins Log.
        /// </summary>
        public static ETProvider ResolveForCurrentSystem(
            ETProvider configured,
            UnityEngine.Object context = null)
        {
            // Die Prozessliste wird nur gelesen, wenn Auto sie wirklich braucht.
            bool varjoLoaderActive = IsVarjoLoaderActive();
            bool sranipalRuntimeRunning = configured == ETProvider.Auto
                && !varjoLoaderActive
                && IsSRanipalRuntimeRunning();
            ETProvider active = Resolve(configured, varjoLoaderActive, sranipalRuntimeRunning);

            Debug.Log(
                $"XR-Loader aktiv: {DescribeActiveXrLoader()}. " +
                $"Eye-Tracker: {active} (eingestellt: {configured}).",
                context);

            if (configured == ETProvider.Auto && active == ETProvider.Dummy)
            {
                Debug.LogWarning(
                    "Eye-Tracker-Auswahl Auto: Es läuft weder der Varjo-Loader noch die " +
                    "SRanipal-Runtime (sr_runtime.exe). Deshalb wird der Dummy verwendet, " +
                    "dessen Blick der Maus folgt. Das ist nur für einen Test am Laptop " +
                    "gedacht; eine Sitzung mit XR-Headset oder mit Require Fixation wird " +
                    "nicht gestartet.",
                    context);
            }

            return active;
        }

        /// <summary>
        /// Auto darf nur dann mit dem Dummy in eine Sitzung gehen, wenn kein
        /// XR-Gerät läuft und die Fixationskontrolle ausgeschaltet ist (reiner
        /// Tastaturtest). Ein ausdrücklich eingestellter Dummy ist nicht betroffen.
        /// </summary>
        public static bool IsDummyFallbackBlocked(
            ETProvider configured,
            ETProvider active,
            bool xrDeviceActive,
            bool requireFixation)
        {
            return configured == ETProvider.Auto
                && active == ETProvider.Dummy
                && (xrDeviceActive || requireFixation);
        }

        /// <summary>
        /// Prüfung für den Sitzungsstart (F5) und den Experimenter Monitor.
        /// </summary>
        public static bool TryGetSessionBlockReason(
            EyeTrackingToolbox toolbox,
            bool requireFixation,
            out string reason)
        {
            reason = null;
            if (toolbox == null)
            {
                return false;
            }

            bool xrDeviceActive = IsXrDeviceActive();
            if (!IsDummyFallbackBlocked(
                toolbox.Provider, toolbox.ActiveProvider, xrDeviceActive, requireFixation))
            {
                return false;
            }

            string cause = xrDeviceActive
                ? $"ein XR-Headset aktiv ist (XR-Loader: {GetActiveXrLoaderName()})"
                : "Require Fixation eingeschaltet ist";
            reason =
                "Sitzungsstart blockiert: Die Eye-Tracker-Auswahl Auto hat keinen echten " +
                $"Eye Tracker gefunden und nutzt den Dummy, obwohl {cause}. " +
                "Vive Pro Eye: SRanipal-Runtime (sr_runtime.exe) starten und den Play " +
                "Mode neu starten. Varjo: Varjo Base starten und den Play Mode neu " +
                "starten. Nur für einen Tastaturtest ohne Headset: Require Fixation " +
                "ausschalten oder den Provider ausdrücklich auf Dummy stellen.";
            return true;
        }

        /// <summary>
        /// Marker für die Aufzeichnung, damit in jeder Messdatei steht, welcher
        /// Eye Tracker und welcher XR-Loader verwendet wurden.
        /// </summary>
        public static string BuildProviderMarker(ETProvider configured, ETProvider active)
        {
            return $"EyeTrackingProvider;configured={configured};active={active};" +
                $"xr_loader={GetActiveXrLoaderName()}";
        }

        public static bool IsVarjoLoaderActive()
        {
            return GetActiveLoader() is VarjoLoader;
        }

        public static string GetActiveXrLoaderName()
        {
            XRLoader loader = GetActiveLoader();
            return loader != null ? loader.GetType().Name : NoLoaderName;
        }

        /// <summary>
        /// Läuft gerade ein XR-Gerät? Maßgeblich ist der aktive XR-Loader; die
        /// Display-Subsysteme fangen den Fall ab, dass XR von Hand gestartet wurde.
        /// </summary>
        public static bool IsXrDeviceActive()
        {
            if (GetActiveLoader() != null)
            {
                return true;
            }

            SubsystemManager.GetSubsystems(displaySubsystems);
            foreach (XRDisplaySubsystem display in displaySubsystems)
            {
                if (display != null && display.running)
                {
                    return true;
                }
            }

            return false;
        }

        public static bool IsSRanipalRuntimeRunning()
        {
            return IsProcessRunning(SRanipalRuntimeProcessName);
        }

        public static bool IsProcessRunning(string processName)
        {
            try
            {
                Process[] processes = Process.GetProcessesByName(processName);
                foreach (Process process in processes)
                {
                    process.Dispose();
                }

                return processes.Length > 0;
            }
            catch (Exception)
            {
                // Lässt sich die Prozessliste nicht lesen, gilt die Runtime als
                // nicht vorhanden. Ein Absturz beim Start wäre schlimmer.
                return false;
            }
        }

        private static string DescribeActiveXrLoader()
        {
            XRLoader loader = GetActiveLoader();
            if (loader == null)
            {
                return NoLoaderName + " (kein XR-Headset)";
            }

            if (loader is OpenXRLoader)
            {
                // Zeigt zum Beispiel "SteamVR/OpenXR". So ist im Log sichtbar, ob am
                // Vive-PC wirklich SteamVR als OpenXR-Runtime eingestellt ist.
                return $"{loader.GetType().Name} (OpenXR-Runtime: {GetOpenXrRuntimeName()})";
            }

            return loader.GetType().Name;
        }

        private static string GetOpenXrRuntimeName()
        {
            try
            {
                string name = OpenXRRuntime.name;
                return string.IsNullOrEmpty(name) ? "unbekannt" : name;
            }
            catch (Exception)
            {
                return "unbekannt";
            }
        }

        private static XRLoader GetActiveLoader()
        {
            XRGeneralSettings settings = XRGeneralSettings.Instance;
            if (settings == null || settings.Manager == null)
            {
                return null;
            }

            return settings.Manager.activeLoader;
        }
    }
}
