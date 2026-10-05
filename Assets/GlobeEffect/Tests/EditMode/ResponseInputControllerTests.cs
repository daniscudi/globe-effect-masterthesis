using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.XR.OpenXR.Input;
using Varjo.XR.Input;
using Mapping = GlobeEffect.VRCheckerboard.VrControllerMapping;
using Response = GlobeEffect.VRCheckerboard.CheckerboardCurvatureResponse;
using ViveController =
    UnityEngine.XR.OpenXR.Features.Interactions.HTCViveControllerProfile.ViveController;

namespace GlobeEffect.VRCheckerboard.Tests
{
    /// <summary>
    /// Prüft die Zuordnung zwischen Controller-Tasten und Antworten und die
    /// Texte, die der Person diese Zuordnung zeigen. Beide Experimente benutzen
    /// dieselbe Logik, deshalb genügt hier ein Controller.
    /// </summary>
    public sealed class ResponseInputControllerTests
    {
        private GameObject controllerObject;
        private CheckerboardKeyboardController controller;

        [SetUp]
        public void SetUp()
        {
            controllerObject = new GameObject("Response input test");
            controller = controllerObject.AddComponent<CheckerboardKeyboardController>();
            controller.SetResponseKeys(Key.DownArrow, Key.UpArrow);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(controllerObject);
        }

        [TestCase(Mapping.TrackpadConcaveTriggerConvex, VrControllerButton.Trigger, Response.Convex)]
        [TestCase(Mapping.TrackpadConcaveTriggerConvex, VrControllerButton.TrackpadClick, Response.Concave)]
        [TestCase(Mapping.TrackpadConvexTriggerConcave, VrControllerButton.Trigger, Response.Concave)]
        [TestCase(Mapping.TrackpadConvexTriggerConcave, VrControllerButton.TrackpadClick, Response.Convex)]
        public void Mapping_AssignsEachButtonTheSelectedResponse(
            Mapping mapping, VrControllerButton button, Response expected)
        {
            Assert.That(VrControllerResponses.ResponseFor(button, mapping), Is.EqualTo(expected));

            // Rückwärts muss dieselbe Taste herauskommen.
            Assert.That(VrControllerResponses.ButtonFor(expected, mapping), Is.EqualTo(button));
        }

        [Test]
        public void MappingValues_KeepTheMeaningOfTheOldSwapCheckbox()
        {
            // Die Szene speicherte früher ein Häkchen: aus = Trigger konvex,
            // an = Trigger konkav. Die Auswahl muss dieselben Zahlen behalten.
            Assert.That((int)Mapping.TrackpadConcaveTriggerConvex, Is.EqualTo(0));
            Assert.That((int)Mapping.TrackpadConvexTriggerConcave, Is.EqualTo(1));
        }

        [Test]
        public void ShownControls_FollowTheSelectedMapping()
        {
            controller.SetVrControllerMapping(Mapping.TrackpadConcaveTriggerConvex);
            Assert.That(controller.GetResponseControlName(Response.Convex), Is.EqualTo("TRIGGER"));
            Assert.That(controller.GetResponseControlName(Response.Concave), Is.EqualTo("TRACKPAD CLICK"));
            Assert.That(controller.BuildResponseLines("A / OUTWARD", "B / INWARD"),
                Is.EqualTo("TRIGGER = A / OUTWARD\nTRACKPAD CLICK = B / INWARD"));

            controller.SetVrControllerMapping(Mapping.TrackpadConvexTriggerConcave);
            Assert.That(controller.GetResponseControlName(Response.Convex), Is.EqualTo("TRACKPAD CLICK"));
            Assert.That(controller.GetResponseControlName(Response.Concave), Is.EqualTo("TRIGGER"));
            Assert.That(controller.BuildResponseLines("A / OUTWARD", "B / INWARD"),
                Is.EqualTo("TRACKPAD CLICK = A / OUTWARD\nTRIGGER = B / INWARD"));
            Assert.That(controller.ControllerSummary,
                Is.EqualTo("TRACKPAD CLICK = konvex, TRIGGER = konkav"));
        }

        [Test]
        public void WithoutVrController_TheKeyboardKeysAreShown()
        {
            controller.SetVrControllerButtonsEnabled(false);

            Assert.That(controller.BuildResponseLines("A", "B"), Is.EqualTo("UP = A\nDOWN = B"));
            Assert.That(controller.ControllerSummary, Is.EqualTo("aus"));
        }

        [Test]
        public void KeyboardKeys_StayAvailableNextToTheController()
        {
            // Die Controller-Zuordnung ändert nichts an der Tastatur.
            controller.SetVrControllerMapping(Mapping.TrackpadConvexTriggerConcave);

            Assert.That(controller.GetKeyName(Response.Convex), Is.EqualTo("UP"));
            Assert.That(controller.GetKeyName(Response.Concave), Is.EqualTo("DOWN"));
            Assert.That(controller.KeyboardSummary, Is.EqualTo("UP = konvex, DOWN = konkav"));
        }

        [Test]
        public void SubmitResponse_ReportsEachAnswerOnceAndIgnoresNone()
        {
            var received = new List<Response>();
            controller.ResponseSubmitted += received.Add;

            controller.SubmitResponse(Response.None);
            controller.SubmitResponse(Response.Convex);

            Assert.That(received, Is.EqualTo(new[] { Response.Convex }));
        }
    }

    /// <summary>
    /// Prüft mit den echten Controller-Layouts von OpenXR (Vive Pro Eye) und
    /// Varjo, dass nur der Moment des Drückens zählt. Die Eingaben werden dem
    /// Input System vorgespielt; ein Controller muss nicht angeschlossen sein.
    /// </summary>
    public sealed class VrControllerPressTests : InputTestFixture
    {
        public override void Setup()
        {
            base.Setup();

            // Das Testgerüst kennt nur die eingebauten Layouts. Die Layouts der
            // beiden XR-Plugins und ihre Bausteine werden deshalb hier angemeldet.
            InputSystem.RegisterLayout<HapticControl>("Haptic");
            InputSystem.RegisterLayout(
                typeof(ViveController).GetProperty(nameof(ViveController.devicePose)).PropertyType, "Pose");
            InputSystem.RegisterLayout<ViveController>();
            InputSystem.RegisterLayout<VarjoViveWand>();

            // Am echten Headset baut das XR-Plugin das Trackpad als "Stick" auf, und
            // genau das erwartet das OpenXR-Layout beim Einrichten. Ohne Headset
            // muss der Test diese eine Angabe selbst nachtragen.
            InputSystem.RegisterLayoutOverride(
                "{ \"name\": \"ViveControllerTrackpadAsStick\", \"extend\": \"ViveController\", " +
                "\"controls\": [ { \"name\": \"trackpad\", \"layout\": \"Stick\" } ] }");
        }

        [Test]
        public void OpenXrViveController_TriggerCountsOnlyWhenItIsPressed()
        {
            ViveController device = InputSystem.AddDevice<ViveController>();
            AssertOnlyThePressCounts(device.triggerPressed, VrControllerButton.Trigger);
        }

        [Test]
        public void OpenXrViveController_TrackpadClickCountsOnlyWhenItIsPressed()
        {
            ViveController device = InputSystem.AddDevice<ViveController>();
            AssertOnlyThePressCounts(device.trackpadClicked, VrControllerButton.TrackpadClick);
        }

        [Test]
        public void VarjoViveWand_TriggerCountsOnlyWhenItIsPressed()
        {
            VarjoViveWand device = InputSystem.AddDevice<VarjoViveWand>();
            AssertOnlyThePressCounts(device.triggerPressed, VrControllerButton.Trigger);
        }

        [Test]
        public void VarjoViveWand_TrackpadClickCountsOnlyWhenItIsPressed()
        {
            VarjoViveWand device = InputSystem.AddDevice<VarjoViveWand>();
            AssertOnlyThePressCounts(device.trackpadPressed, VrControllerButton.TrackpadClick);
        }

        [Test]
        public void TouchingTheTrackpad_IsNotAnAnswer()
        {
            ViveController openXr = InputSystem.AddDevice<ViveController>();
            VarjoViveWand varjo = InputSystem.AddDevice<VarjoViveWand>();

            Press(openXr.trackpadTouched);
            Assert.That(VrControllerResponses.TryReadPress(out _), Is.False);
            Press(varjo.trackpadTouched);
            Assert.That(VrControllerResponses.TryReadPress(out _), Is.False);
        }

        [Test]
        public void TwoControllersPressedInTheSameFrame_GiveOneAnswer()
        {
            ViveController left = InputSystem.AddDevice<ViveController>();
            ViveController right = InputSystem.AddDevice<ViveController>();

            Press(left.triggerPressed, queueEventOnly: true);
            Press(right.trackpadClicked, queueEventOnly: true);
            InputSystem.Update();

            // Gemeldet wird genau ein Tastendruck. Der Experiment Manager nimmt
            // pro Durchgang ohnehin nur die erste Antwort an.
            Assert.That(VrControllerResponses.TryReadPress(out VrControllerButton button), Is.True);
            Assert.That(button, Is.EqualTo(VrControllerButton.Trigger));
        }

        private void AssertOnlyThePressCounts(ButtonControl control, VrControllerButton expected)
        {
            Assert.That(VrControllerResponses.TryReadPress(out _), Is.False, "vor dem Drücken");

            Press(control);
            Assert.That(VrControllerResponses.TryReadPress(out VrControllerButton button), Is.True);
            Assert.That(button, Is.EqualTo(expected));

            // Im nächsten Frame wird die Taste nur noch gehalten.
            InputSystem.Update();
            Assert.That(VrControllerResponses.TryReadPress(out _), Is.False, "beim Halten");

            Release(control);
            Assert.That(VrControllerResponses.TryReadPress(out _), Is.False, "beim Loslassen");
        }
    }
}
