using System;
using System.Threading;
using Nefarius.ViGEm.Client;
using Nefarius.ViGEm.Client.Targets;
using Nefarius.ViGEm.Client.Targets.Xbox360;
using SharpDX.DirectInput;
using MathNet;
using GTAV_WheelControls;
using System.Runtime.InteropServices;
using WindowsInput;
using System.Collections.Generic;

namespace WheelFeeder
{
    internal class Program
    {
        private static bool isWheelSteeringOn = true;
        private static bool isOnReverse = false;
        private static bool isPrecisionFlyingModeActivated = false;

        private static int maxAxisValue = 32767;
        private static int minAxisValue = -32767;

        private static int maxDeadzoneOffset = 7300;
        private static int minDeadzoneOffset = -7300;

        private static float wheelmultiplier = 1.30F;

        private static List<string> modes = new List<string>();
        private static byte mode = 1;

        private static Action<IXbox360Controller, JoystickUpdate, InputSimulator> ExecuteCurrentModeKeybinds;

        private static IDictionary<string, WheelButtons> WheelButtonDict = new Dictionary<string, WheelButtons>();

        private enum WheelButtons
        {
            LeftDirectional,
            RightDirectional,
            Triangle,
            Square,
            Circle,
            X,
            Dpad,
            SE,
            ST,
            R2,
            L2,
            L3,
            R3,
            PS,
            Thrust,
            Break,
            Clutch,
            Steering
        }

        private static void Main(string[] args)
        {
            WheelButtonDict.Add("Buttons0", WheelButtons.LeftDirectional);
            WheelButtonDict.Add("Buttons1", WheelButtons.RightDirectional);
            WheelButtonDict.Add("Buttons2", WheelButtons.Triangle);
            WheelButtonDict.Add("Buttons3", WheelButtons.Square);
            WheelButtonDict.Add("Buttons4", WheelButtons.Circle);
            WheelButtonDict.Add("Buttons5", WheelButtons.X);
            WheelButtonDict.Add("PointOfViewControllers0", WheelButtons.Dpad);
            WheelButtonDict.Add("Buttons6", WheelButtons.SE);
            WheelButtonDict.Add("Buttons7", WheelButtons.ST);
            WheelButtonDict.Add("Buttons8", WheelButtons.R2);
            WheelButtonDict.Add("Buttons9", WheelButtons.L2);
            WheelButtonDict.Add("Buttons10", WheelButtons.L3);
            WheelButtonDict.Add("Buttons11", WheelButtons.R3);
            WheelButtonDict.Add("Buttons12", WheelButtons.PS);
            WheelButtonDict.Add("RotationZ", WheelButtons.Thrust);
            WheelButtonDict.Add("Y", WheelButtons.Break);
            WheelButtonDict.Add("Sliders0", WheelButtons.Clutch);
            WheelButtonDict.Add("X", WheelButtons.Steering);

            modes.Add("Motorcycles");
            modes.Add("Cars");
            modes.Add("Helicopters");
            modes.Add("Planes");

            ViGEmClient client = new ViGEmClient();
            IXbox360Controller controller = client.CreateXbox360Controller();
            controller.Connect();

            var directInput = new DirectInput();

            var wheelGuid = Guid.Empty;
            var gearboxGuid = Guid.Empty;

            Console.WriteLine($@"Waiting for GTAV process...");
            while (Memory.GTAprocess == null)
            {
                Memory.GetGTAProcess();
                Thread.Sleep(1000);
            }
            Console.WriteLine($@"Found {Memory.GTAprocess.ProcessName}");

            IList<DeviceInstance> a = directInput.GetDevices(DeviceType.Driving, DeviceEnumerationFlags.AllDevices);

            foreach (DeviceInstance deviceInstance in directInput.GetDevices(DeviceType.Driving, DeviceEnumerationFlags.AllDevices))
            {
                Console.WriteLine($@"deviceInstance.ProductName {deviceInstance.ProductName}");
                Console.WriteLine($@"deviceInstance.GetType() {deviceInstance.GetType()}");
                Console.WriteLine($@"deviceInstance.ProductGuid {deviceInstance.ProductGuid}");
                Console.WriteLine($@"deviceInstance.InstanceGuid {deviceInstance.InstanceGuid}");
                Console.WriteLine($@"deviceInstance.InstanceName {deviceInstance.InstanceName}");
                Console.WriteLine($@"deviceInstance.Usage {deviceInstance.Usage}");
                wheelGuid = deviceInstance.InstanceGuid;
            }

            foreach (var deviceInstance in directInput.GetDevices(DeviceType.Joystick, DeviceEnumerationFlags.AllDevices))
            {
                Console.WriteLine($@"deviceInstance.ProductName {deviceInstance.ProductName}");
                Console.WriteLine($@"deviceInstance.GetType() {deviceInstance.GetType()}");
                Console.WriteLine($@"deviceInstance.ProductGuid {deviceInstance.ProductGuid}");
                Console.WriteLine($@"deviceInstance.InstanceGuid {deviceInstance.InstanceGuid}");
                Console.WriteLine($@"deviceInstance.InstanceName {deviceInstance.InstanceName}");
                Console.WriteLine($@"deviceInstance.Usage {deviceInstance.Usage}");
                gearboxGuid = deviceInstance.InstanceGuid;
            }

            //if (wheelGuid == Guid.Empty)
            //{
            //    Console.WriteLine("No joystick/Gamepad found.");
            //    Console.ReadKey();
            //    Environment.Exit(1);
            //}
            var gearbox = new Joystick(directInput, gearboxGuid);
            var wheel = new Joystick(directInput, wheelGuid);

            Console.WriteLine("Found wheel with GUID: {0}", wheelGuid);
            Console.WriteLine("Found gearbox with GUID: {0}", gearboxGuid);

            var allEffects = wheel.GetEffects();
            foreach (var effectInfo in allEffects)
            {
                Console.WriteLine("Wheel effect available {0}", effectInfo.Name);
            }

            allEffects = gearbox.GetEffects();
            foreach (var effectInfo in allEffects)
            {
                Console.WriteLine("gearbox effect available {0}", effectInfo.Name);
            }

            wheel.Properties.BufferSize = 128;
            wheel.Acquire();

            gearbox.Properties.BufferSize = 128;
            gearbox.Acquire();

            CruiseControl.Initialize(controller);
            InputSimulator input = new InputSimulator();

            ApplyModeSettings(modes[mode]);

            while (true)
            {
                Thread.Sleep(50);
                wheel.Poll();
                gearbox.Poll();
                var wheeldata = wheel.GetBufferedData();
                var gearboxdata = gearbox.GetBufferedData();

                foreach (var state in wheeldata)
                {
                    ExecuteCurrentModeKeybinds(controller, state, input);
                    //Console.WriteLine($@"Wheel: {state} ::: {isWheelSteeringOn}");
                }
                foreach (var state in gearboxdata)
                {
                    if (state.Offset.ToString() == "Buttons6")
                    {
                        CruiseControl.isActivated = false;
                        if (state.Value == 128)
                        {
                            controller.SetSliderValue(Xbox360Slider.RightTrigger, 255);
                        }
                        if (state.Value == 0)
                        {
                            controller.SetSliderValue(Xbox360Slider.RightTrigger, 0);
                        }
                    }
                    else { CruiseControl.UpdateSpeed(state); }
                }
            }

            static void PedalThrustFunc(JoystickUpdate state, IXbox360Controller controller)
            {
                if (state.Value < 65535)
                {
                    byte thrustvalue = (byte)Math.Round((decimal)state.Value / 257);
                    byte invertedthrustvalue = (byte)(short)((255 - thrustvalue));
                    controller.SetSliderValue(Xbox360Slider.RightTrigger, invertedthrustvalue);
                }
                else
                {
                    controller.SetSliderValue(Xbox360Slider.RightTrigger, 0);
                }
            }
            static void PedalBrakeFunc(JoystickUpdate state, IXbox360Controller controller)
            {
                if (state.Value < 65535)
                {
                    byte thrustvalue = (byte)Math.Round((decimal)state.Value / 257);
                    byte invertedthrustvalue = (byte)(short)((255 - thrustvalue));
                    controller.SetSliderValue(Xbox360Slider.LeftTrigger, invertedthrustvalue);
                    //Console.WriteLine($@"Thrust: {state.Value} Thrust/2: {thrustvalue} Inverted: {invertedthrustvalue}");
                }
                else
                {
                    controller.SetSliderValue(Xbox360Slider.LeftTrigger, 0);
                }
            }
            static void WheelMovementFunc(JoystickUpdate state, IXbox360Controller controller, bool iswheelsteeringactivated)
            {
                if (!iswheelsteeringactivated) { return; }
                //int maxWheelRotationToTheRight = 65535;
                //int maxWheelRotationToTheLeft = 0;
                //int centeredWheelValue = 32767;
                int currentWheelValue = state.Value;

                double value;
                value = (currentWheelValue - maxAxisValue) * wheelmultiplier;
                //value = currentWheelValue - maxAxisValue;

                if (value > 0) { value = maxDeadzoneOffset + value; };
                if (value < 0) { value = minDeadzoneOffset + value; };

                if (value > maxAxisValue) { value = maxAxisValue; };
                if (value < minAxisValue) { value = minAxisValue; };

                //var curve = new MathNet.Numerics.Distributions.Normal();
                //var z_value = curve.InverseCumulativeDistribution(value);

                //Console.WriteLine($@"{state.Value} -> {(short)value}");
                controller.SetAxisValue(Xbox360Axis.LeftThumbX, (short)value);
            }

            static void NextMode()
            {
                mode += 1;
                if (mode >= modes.Count) { mode = 0; }
                ApplyModeSettings(modes[mode]);
                Console.WriteLine($@"{modes[mode]}");
            }
            static void ApplyModeSettings(string mode)
            {
                switch (mode)
                {
                    case "Motorcycles":
                        {
                            maxDeadzoneOffset = 8300;
                            minDeadzoneOffset = -maxDeadzoneOffset;
                            wheelmultiplier = 1.50F;
                            ExecuteCurrentModeKeybinds = delegate (IXbox360Controller controller, JoystickUpdate state, InputSimulator input)
                            {
                                if (!WheelButtonDict.TryGetValue(state.Offset.ToString(), out WheelButtons btn)) { Console.WriteLine("Dictionary Error"); return; };
                                switch (btn)
                                {
                                    case WheelButtons.LeftDirectional:
                                        ControllerButtonPress(controller, state, input, WindowsInput.Native.VirtualKeyCode.OEM_4);
                                        break;

                                    case WheelButtons.RightDirectional:
                                        ControllerButtonPress(controller, state, input, WindowsInput.Native.VirtualKeyCode.OEM_6);
                                        break;

                                    case WheelButtons.Triangle:
                                        break;

                                    case WheelButtons.Square:
                                        break;

                                    case WheelButtons.Circle:
                                        break;

                                    case WheelButtons.X:
                                        ControllerButtonPress(controller, state, input, WindowsInput.Native.VirtualKeyCode.VK_K);
                                        break;

                                    case WheelButtons.Dpad:
                                        if (state.Value == 0 || state.Value == -1) { controller.SetButtonState(Xbox360Button.RightThumb, false); controller.SetAxisValue(Xbox360Axis.RightThumbX, 0); controller.SetAxisValue(Xbox360Axis.RightThumbY, 0); } //up
                                        else if (state.Value == 27000) { controller.SetAxisValue(Xbox360Axis.RightThumbX, (short)-25000); } //left
                                        else if (state.Value == 9000) { controller.SetAxisValue(Xbox360Axis.RightThumbX, (short)25000); } //right
                                        else if (state.Value == 18000) { controller.SetButtonState(Xbox360Button.RightThumb, true); }
                                        break;

                                    case WheelButtons.SE:
                                        if (state.Value == 128) { NextMode(); }
                                        break;

                                    case WheelButtons.ST:
                                        break;

                                    case WheelButtons.L2:
                                        if (state.Value == 128)
                                        { controller.SetAxisValue(Xbox360Axis.LeftThumbY, (short)minAxisValue); }
                                        if (state.Value == 0)
                                        { controller.SetAxisValue(Xbox360Axis.LeftThumbY, 0); }
                                        break;

                                    case WheelButtons.R2:
                                        if (state.Value == 128)
                                        { controller.SetAxisValue(Xbox360Axis.LeftThumbY, (short)maxAxisValue); }
                                        if (state.Value == 0)
                                        { controller.SetAxisValue(Xbox360Axis.LeftThumbY, 0); }
                                        break;

                                    case WheelButtons.L3:
                                        break;

                                    case WheelButtons.R3:
                                        break;

                                    case WheelButtons.PS:
                                        if (state.Value == 128) { isWheelSteeringOn = !isWheelSteeringOn; controller.SetAxisValue(Xbox360Axis.LeftThumbX, 0); }
                                        break;

                                    case WheelButtons.Thrust:
                                        PedalThrustFunc(state, controller);
                                        break;

                                    case WheelButtons.Break:
                                        PedalBrakeFunc(state, controller);
                                        break;

                                    case WheelButtons.Clutch:
                                        break;

                                    case WheelButtons.Steering:
                                        WheelMovementFunc(state, controller, isWheelSteeringOn);
                                        break;
                                }
                            };
                            break;
                        }
                    case "Cars":
                        {
                            maxDeadzoneOffset = 8000;
                            minDeadzoneOffset = -maxDeadzoneOffset;
                            wheelmultiplier = 2F;
                            ExecuteCurrentModeKeybinds = delegate (IXbox360Controller controller, JoystickUpdate state, InputSimulator input)
                            {
                                if (!WheelButtonDict.TryGetValue(state.Offset.ToString(), out WheelButtons btn)) { Console.WriteLine("Dictionary Error"); return; };
                                switch (btn)
                                {
                                    case WheelButtons.LeftDirectional:
                                        ControllerButtonPress(controller, state, input, WindowsInput.Native.VirtualKeyCode.OEM_4);
                                        break;

                                    case WheelButtons.RightDirectional:
                                        ControllerButtonPress(controller, state, input, WindowsInput.Native.VirtualKeyCode.OEM_6);
                                        break;

                                    case WheelButtons.Triangle:

                                        break;

                                    case WheelButtons.Square:
                                        break;

                                    case WheelButtons.Circle:
                                        break;

                                    case WheelButtons.X:
                                        ControllerButtonPress(controller, state, input, WindowsInput.Native.VirtualKeyCode.VK_K);
                                        break;

                                    case WheelButtons.Dpad:
                                        if (state.Value == 0 || state.Value == -1) { controller.SetButtonState(Xbox360Button.RightThumb, false); controller.SetAxisValue(Xbox360Axis.RightThumbX, 0); controller.SetAxisValue(Xbox360Axis.RightThumbY, 0); } //up
                                        else if (state.Value == 27000) { controller.SetAxisValue(Xbox360Axis.RightThumbX, (short)-25000); } //left
                                        else if (state.Value == 9000) { controller.SetAxisValue(Xbox360Axis.RightThumbX, (short)25000); } //right
                                        else if (state.Value == 18000) { controller.SetButtonState(Xbox360Button.RightThumb, true); }
                                        break;

                                    case WheelButtons.SE:
                                        if (state.Value == 128) { NextMode(); }
                                        break;

                                    case WheelButtons.ST:
                                        break;

                                    case WheelButtons.L2:
                                        if (state.Value == 128)
                                        { controller.SetAxisValue(Xbox360Axis.LeftThumbY, (short)minAxisValue); }
                                        if (state.Value == 0)
                                        { controller.SetAxisValue(Xbox360Axis.LeftThumbY, 0); }
                                        break;

                                    case WheelButtons.R2:
                                        if (state.Value == 128)
                                        { controller.SetAxisValue(Xbox360Axis.LeftThumbY, (short)maxAxisValue); }
                                        if (state.Value == 0)
                                        { controller.SetAxisValue(Xbox360Axis.LeftThumbY, 0); }
                                        break;

                                    case WheelButtons.L3:
                                        break;

                                    case WheelButtons.R3:
                                        break;

                                    case WheelButtons.PS:
                                        if (state.Value == 128) { isWheelSteeringOn = !isWheelSteeringOn; controller.SetAxisValue(Xbox360Axis.LeftThumbX, 0); }
                                        break;

                                    case WheelButtons.Thrust:
                                        PedalThrustFunc(state, controller);
                                        break;

                                    case WheelButtons.Break:
                                        PedalBrakeFunc(state, controller);
                                        break;

                                    case WheelButtons.Clutch:
                                        break;

                                    case WheelButtons.Steering:
                                        WheelMovementFunc(state, controller, isWheelSteeringOn);
                                        break;
                                }
                            };
                            break;
                        }
                    case "Helicopters":
                        {
                            maxDeadzoneOffset = 7300;
                            minDeadzoneOffset = -maxDeadzoneOffset;
                            wheelmultiplier = 2F;
                            ExecuteCurrentModeKeybinds = delegate (IXbox360Controller controller, JoystickUpdate state, InputSimulator input)
                            {
                                if (!WheelButtonDict.TryGetValue(state.Offset.ToString(), out WheelButtons btn)) { Console.WriteLine("Dictionary Error"); return; };
                                switch (btn)
                                {
                                    case WheelButtons.LeftDirectional:
                                        if (state.Value == 128)
                                        { controller.SetAxisValue(Xbox360Axis.LeftThumbY, (short)minAxisValue); }
                                        if (state.Value == 0)
                                        { controller.SetAxisValue(Xbox360Axis.LeftThumbY, 0); }
                                        break;

                                    case WheelButtons.RightDirectional:
                                        if (state.Value == 128)
                                        { controller.SetAxisValue(Xbox360Axis.LeftThumbY, (short)maxAxisValue); }
                                        if (state.Value == 0)
                                        { controller.SetAxisValue(Xbox360Axis.LeftThumbY, 0); }
                                        break;

                                    case WheelButtons.Triangle:
                                        if (state.Value == 128) { controller.SetButtonState(Xbox360Button.Right, true); }
                                        if (state.Value == 0) { controller.SetButtonState(Xbox360Button.Right, false); }
                                        break;

                                    case WheelButtons.Square:
                                        if (state.Value == 128) { controller.SetButtonState(Xbox360Button.LeftThumb, true); }
                                        if (state.Value == 0) { controller.SetButtonState(Xbox360Button.LeftThumb, false); }
                                        break;

                                    case WheelButtons.Circle:
                                        if (state.Value == 128) { isPrecisionFlyingModeActivated = !isPrecisionFlyingModeActivated; }
                                        break;

                                    case WheelButtons.X:
                                        ControllerButtonPress(controller, state, input, WindowsInput.Native.VirtualKeyCode.VK_K);
                                        break;

                                    case WheelButtons.Dpad:
                                        if (state.Value == 0 || state.Value == -1) { controller.SetButtonState(Xbox360Button.RightThumb, false); controller.SetAxisValue(Xbox360Axis.RightThumbX, 0); controller.SetAxisValue(Xbox360Axis.RightThumbY, 0); } //up
                                        else if (state.Value == 27000) { controller.SetAxisValue(Xbox360Axis.RightThumbX, (short)-25000); } //left
                                        else if (state.Value == 9000) { controller.SetAxisValue(Xbox360Axis.RightThumbX, (short)25000); } //right
                                        else if (state.Value == 18000) { controller.SetButtonState(Xbox360Button.RightThumb, true); }
                                        break;

                                    case WheelButtons.SE:
                                        if (state.Value == 128) { NextMode(); }
                                        break;

                                    case WheelButtons.ST:
                                        if (state.Value == 128) { isOnReverse = !isOnReverse; }
                                        //if (state.Value == 0) { isOnReverse = false; }
                                        break;

                                    case WheelButtons.L2:
                                        if (state.Value == 128)
                                        { controller.SetButtonState(Xbox360Button.LeftShoulder, true); }
                                        if (state.Value == 0)
                                        { controller.SetButtonState(Xbox360Button.LeftShoulder, false); }
                                        break;

                                    case WheelButtons.R2:
                                        if (state.Value == 128)
                                        { controller.SetButtonState(Xbox360Button.RightShoulder, true); }
                                        if (state.Value == 0)
                                        { controller.SetButtonState(Xbox360Button.RightShoulder, false); }
                                        break;

                                    case WheelButtons.L3:
                                        break;

                                    case WheelButtons.R3:
                                        break;

                                    case WheelButtons.PS:
                                        if (state.Value == 128) { isWheelSteeringOn = !isWheelSteeringOn; controller.SetAxisValue(Xbox360Axis.LeftThumbX, 0); }
                                        break;

                                    case WheelButtons.Thrust:
                                        if (state.Value < 65535)
                                        {
                                            int currentWheelValue = state.Value;
                                            double value;

                                            value = (65535 - state.Value) / 2;

                                            var curve = new MathNet.Numerics.Distributions.Normal(65535, 10);
                                            var z_value = curve.InverseCumulativeDistribution(value);

                                            if (value > maxAxisValue) { value = maxAxisValue; }
                                            if (value < 0) { value = 0; }

                                            if (isPrecisionFlyingModeActivated)
                                            {
                                                controller.SetAxisValue(Xbox360Axis.LeftThumbY, (short)(value / 2));
                                            }
                                            else
                                            {
                                                controller.SetAxisValue(Xbox360Axis.LeftThumbY, (short)(value));
                                            }
                                        }
                                        else
                                        {
                                            controller.SetAxisValue(Xbox360Axis.LeftThumbY, 0);
                                        }
                                        break;

                                    case WheelButtons.Break:
                                        if (state.Value < 65535)
                                        {
                                            int currentWheelValue = state.Value;
                                            double value;

                                            value = (65535 - state.Value) / 2;

                                            var curve = new MathNet.Numerics.Distributions.Normal(65535, 10);
                                            var z_value = curve.InverseCumulativeDistribution(value);

                                            if (value > maxAxisValue) { value = maxAxisValue; }
                                            if (value < 0) { value = 0; }

                                            if (isPrecisionFlyingModeActivated)
                                            {
                                                controller.SetAxisValue(Xbox360Axis.LeftThumbY, (short)(-value / 2));
                                            }
                                            else
                                            {
                                                controller.SetAxisValue(Xbox360Axis.LeftThumbY, (short)(-value));
                                            }
                                            Console.WriteLine();
                                        }
                                        else
                                        {
                                            controller.SetAxisValue(Xbox360Axis.LeftThumbY, 0);
                                        }
                                        break;

                                    case WheelButtons.Clutch:
                                        if (state.Value < 65535)
                                        {
                                            byte thrustvalue = (byte)Math.Round((decimal)state.Value / 257);
                                            byte invertedthrustvalue = (byte)(short)((255 - thrustvalue));

                                            Console.WriteLine(invertedthrustvalue);

                                            if (isOnReverse)
                                            {
                                                controller.SetSliderValue(Xbox360Slider.RightTrigger, 0);
                                                controller.SetSliderValue(Xbox360Slider.LeftTrigger, invertedthrustvalue);
                                            }
                                            if (!isOnReverse)
                                            {
                                                controller.SetSliderValue(Xbox360Slider.LeftTrigger, 0);
                                                controller.SetSliderValue(Xbox360Slider.RightTrigger, invertedthrustvalue);
                                            }

                                            //controller.SetSliderValue(Xbox360Slider.LeftTrigger, invertedthrustvalue);
                                            //Console.WriteLine($@"Thrust: {state.Value} Thrust/2: {thrustvalue} Inverted: {invertedthrustvalue}");
                                        }
                                        else
                                        {
                                            controller.SetSliderValue(Xbox360Slider.RightTrigger, 0);
                                        }
                                        break;

                                    case WheelButtons.Steering:
                                        WheelMovementFunc(state, controller, isWheelSteeringOn);
                                        break;
                                }
                            };
                            break;
                        }
                    case "Planes":
                        {
                            maxDeadzoneOffset = 6000;
                            minDeadzoneOffset = -maxDeadzoneOffset;
                            wheelmultiplier = 1.10F;
                            ExecuteCurrentModeKeybinds = delegate (IXbox360Controller controller, JoystickUpdate state, InputSimulator input)
                            {
                                if (!WheelButtonDict.TryGetValue(state.Offset.ToString(), out WheelButtons btn)) { Console.WriteLine("Dictionary Error"); return; };
                                switch (btn)
                                {
                                    case WheelButtons.LeftDirectional:
                                        if (state.Value == 128)
                                        { controller.SetAxisValue(Xbox360Axis.LeftThumbY, (short)minAxisValue); }
                                        if (state.Value == 0)
                                        { controller.SetAxisValue(Xbox360Axis.LeftThumbY, 0); }
                                        break;

                                    case WheelButtons.RightDirectional:
                                        if (state.Value == 128)
                                        { controller.SetAxisValue(Xbox360Axis.LeftThumbY, (short)maxAxisValue); }
                                        if (state.Value == 0)
                                        { controller.SetAxisValue(Xbox360Axis.LeftThumbY, 0); }
                                        break;

                                    case WheelButtons.Triangle:
                                        if (state.Value == 128) { controller.SetButtonState(Xbox360Button.Right, true); }
                                        if (state.Value == 0) { controller.SetButtonState(Xbox360Button.Right, false); }
                                        break;

                                    case WheelButtons.Square:
                                        if (state.Value == 128) { controller.SetButtonState(Xbox360Button.LeftThumb, true); }
                                        if (state.Value == 0) { controller.SetButtonState(Xbox360Button.LeftThumb, false); }
                                        break;

                                    case WheelButtons.Circle:
                                        if (state.Value == 128) { isPrecisionFlyingModeActivated = !isPrecisionFlyingModeActivated; }
                                        break;

                                    case WheelButtons.X:
                                        ControllerButtonPress(controller, state, input, WindowsInput.Native.VirtualKeyCode.VK_K);
                                        break;

                                    case WheelButtons.Dpad:
                                        if (state.Value == 0 || state.Value == -1) { controller.SetButtonState(Xbox360Button.RightThumb, false); controller.SetAxisValue(Xbox360Axis.RightThumbX, 0); controller.SetAxisValue(Xbox360Axis.RightThumbY, 0); } //up
                                        else if (state.Value == 27000) { controller.SetAxisValue(Xbox360Axis.RightThumbX, (short)-25000); } //left
                                        else if (state.Value == 9000) { controller.SetAxisValue(Xbox360Axis.RightThumbX, (short)25000); } //right
                                        else if (state.Value == 18000) { controller.SetButtonState(Xbox360Button.RightThumb, true); }
                                        break;

                                    case WheelButtons.SE:
                                        if (state.Value == 128) { NextMode(); }
                                        break;

                                    case WheelButtons.ST:
                                        if (state.Value == 128) { isOnReverse = !isOnReverse; }
                                        //if (state.Value == 0) { isOnReverse = false; }
                                        break;

                                    case WheelButtons.L2:
                                        if (state.Value == 128)
                                        { controller.SetButtonState(Xbox360Button.LeftShoulder, true); }
                                        if (state.Value == 0)
                                        { controller.SetButtonState(Xbox360Button.LeftShoulder, false); }
                                        break;

                                    case WheelButtons.R2:
                                        if (state.Value == 128)
                                        { controller.SetButtonState(Xbox360Button.RightShoulder, true); }
                                        if (state.Value == 0)
                                        { controller.SetButtonState(Xbox360Button.RightShoulder, false); }
                                        break;

                                    case WheelButtons.L3:
                                        break;

                                    case WheelButtons.R3:
                                        break;

                                    case WheelButtons.PS:
                                        if (state.Value == 128) { isWheelSteeringOn = !isWheelSteeringOn; controller.SetAxisValue(Xbox360Axis.LeftThumbX, 0); }
                                        break;

                                    case WheelButtons.Thrust:
                                        if (state.Value < 65535)
                                        {
                                            int currentWheelValue = state.Value;
                                            double value;

                                            value = 65535 - state.Value;

                                            var curve = new MathNet.Numerics.Distributions.Normal(65535, 10);
                                            var z_value = curve.InverseCumulativeDistribution(value);

                                            if (value > maxAxisValue) { value = maxAxisValue; }
                                            if (value < 0) { value = 0; }

                                            if (isPrecisionFlyingModeActivated)
                                            {
                                                controller.SetAxisValue(Xbox360Axis.LeftThumbY, (short)(-value / 2));
                                            }
                                            else
                                            {
                                                controller.SetAxisValue(Xbox360Axis.LeftThumbY, (short)(-value));
                                            }
                                        }
                                        else
                                        {
                                            controller.SetAxisValue(Xbox360Axis.LeftThumbY, 0);
                                        }
                                        break;

                                    case WheelButtons.Break:
                                        if (state.Value < 65535)
                                        {
                                            int currentWheelValue = state.Value;
                                            double value;

                                            value = 65535 - state.Value;

                                            var curve = new MathNet.Numerics.Distributions.Normal(65535, 10);
                                            var z_value = curve.InverseCumulativeDistribution(value);

                                            if (value > maxAxisValue) { value = maxAxisValue; }
                                            if (value < 0) { value = 0; }

                                            if (isPrecisionFlyingModeActivated)
                                            {
                                                controller.SetAxisValue(Xbox360Axis.LeftThumbY, (short)(value / 2));
                                            }
                                            else
                                            {
                                                controller.SetAxisValue(Xbox360Axis.LeftThumbY, (short)(value));
                                            }
                                            Console.WriteLine();
                                        }
                                        else
                                        {
                                            controller.SetAxisValue(Xbox360Axis.LeftThumbY, 0);
                                        }
                                        break;

                                    case WheelButtons.Clutch:
                                        if (state.Value < 65535)
                                        {
                                            byte thrustvalue = (byte)Math.Round((decimal)state.Value / 257);
                                            byte invertedthrustvalue = (byte)(short)((255 - thrustvalue));

                                            if (isOnReverse)
                                            {
                                                controller.SetSliderValue(Xbox360Slider.RightTrigger, 0);
                                                controller.SetSliderValue(Xbox360Slider.LeftTrigger, invertedthrustvalue);
                                            }
                                            if (!isOnReverse)
                                            {
                                                controller.SetSliderValue(Xbox360Slider.LeftTrigger, 0);
                                                controller.SetSliderValue(Xbox360Slider.RightTrigger, invertedthrustvalue);
                                            }

                                            //controller.SetSliderValue(Xbox360Slider.LeftTrigger, invertedthrustvalue);
                                            //Console.WriteLine($@"Thrust: {state.Value} Thrust/2: {thrustvalue} Inverted: {invertedthrustvalue}");
                                        }
                                        else
                                        {
                                            controller.SetSliderValue(Xbox360Slider.RightTrigger, 0);
                                        }
                                        break;

                                    case WheelButtons.Steering:
                                        WheelMovementFunc(state, controller, isWheelSteeringOn);
                                        break;
                                }
                            };
                            break;
                        }
                    default:
                        maxDeadzoneOffset = 7300;
                        minDeadzoneOffset = -maxDeadzoneOffset;
                        wheelmultiplier = 1.30F;
                        ExecuteCurrentModeKeybinds = delegate (IXbox360Controller controller, JoystickUpdate state, InputSimulator input)
                        {
                            Console.WriteLine("Mode Error, contact noone");
                        };
                        break;
                }
            }

            static void ControllerButtonPress(IXbox360Controller controller, JoystickUpdate state, InputSimulator inputSimulator, WindowsInput.Native.VirtualKeyCode virtualKeyCode)
            {
                if (state.Value == 128)
                { inputSimulator.Keyboard.KeyDown(virtualKeyCode); }
                if (state.Value == 0)
                { inputSimulator.Keyboard.KeyUp(virtualKeyCode); }
            }
        }
    }
}