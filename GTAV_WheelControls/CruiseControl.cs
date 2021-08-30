using System;
using System.Collections.Generic;
using System.Text;
using Nefarius.ViGEm.Client.Targets;
using Nefarius.ViGEm.Client.Targets.Xbox360;
using SharpDX.DirectInput;

namespace GTAV_WheelControls
{
    public static class CruiseControl
    {
        public static bool isActivated = false;
        public static int cruiseSpeed = 0;
        private static int appliedForce = 0;
        public static JoystickUpdate state;
        public static IXbox360Controller controller;

        private static float lastSpeed = 0;

        public static Dictionary<string, int> gearToSpeed = new Dictionary<string, int>();

        public static void Initialize(IXbox360Controller controllers)
        {
            controller = controllers;

            gearToSpeed.Add("Buttons0", 10);
            gearToSpeed.Add("Buttons1", 20);
            gearToSpeed.Add("Buttons2", 30);
            gearToSpeed.Add("Buttons3", 50);
            gearToSpeed.Add("Buttons4", 80);
            gearToSpeed.Add("Buttons5", 100);

            PeriodicTask.Run(UpdateBot, new TimeSpan(0, 0, 0, 0, 20));
        }

        public static void UpdateBot()
        {
            if (!isActivated || cruiseSpeed == 0) { return; }
            float currentVehicleSpeed = Memory.GetVehicleSpeed();



            float speedDifference = cruiseSpeed - currentVehicleSpeed;
            float newSpeedDiff = currentVehicleSpeed - lastSpeed;


            if (speedDifference > -0.1 && speedDifference < 0.1) { return; }
            else if (currentVehicleSpeed > cruiseSpeed)
            {
                if (newSpeedDiff <= -0.1) { return; }
                if (speedDifference > -0.5 && speedDifference < 0 && newSpeedDiff > 0) { return; }
                appliedForce += -1;

                if (appliedForce > 255) { appliedForce = 255; }
                if (appliedForce < 0) { appliedForce = 0; }
                //Console.WriteLine($@"HIGHER   LAST: {lastSpeed} CURRENT: {currentVehicleSpeed} CRUISE SPEED: { cruiseSpeed } DIFF: {speedDifference} 2ND DIFF: {newSpeedDiff}  APPLIED: {appliedForce}");
            }
            else if (currentVehicleSpeed < cruiseSpeed)
            {
                if (newSpeedDiff >= 0.20) { return; }
                if (speedDifference < 0.5 && speedDifference > 0 && newSpeedDiff > 0.1) { return; }
                if (speedDifference >= 8) { appliedForce += 5; }
                else if (speedDifference >= 6) { appliedForce += 4; }
                else if (speedDifference >= 4) { appliedForce += 3; }
                else if (speedDifference >= 2) { appliedForce += 2; }
                else if (speedDifference > 0) { appliedForce += 1; }

                if (appliedForce > 255) { appliedForce = 255; }
                if (appliedForce < 0) { appliedForce = 0; }
                //Console.WriteLine($@"LOWER   LAST: {lastSpeed} CURRENT: {currentVehicleSpeed} CRUISE SPEED: { cruiseSpeed } DIFF: {speedDifference} 2ND DIFF: {newSpeedDiff} APPLIED: {appliedForce}");
            }
            controller.SetSliderValue(Xbox360Slider.RightTrigger, (byte)appliedForce);
            lastSpeed = currentVehicleSpeed;
        }

        public static void UpdateSpeed(JoystickUpdate state)
        {
            if (state.Value == 128)
            {
                cruiseSpeed = gearToSpeed.GetValueOrDefault(state.Offset.ToString());
                isActivated = true;
                appliedForce = 90;
                controller.SetSliderValue(Xbox360Slider.RightTrigger, (byte)(gearToSpeed.GetValueOrDefault(state.Offset.ToString()) * 2));
                //Console.WriteLine($@"SPEED UPDATE: {cruiseSpeed}");
            }
            else
            {
                isActivated = false;
                cruiseSpeed = 0;
                controller.SetSliderValue(Xbox360Slider.RightTrigger, 0);
            }
        }
    }
}
