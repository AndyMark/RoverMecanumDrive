//CSK 10/2016 Orignally Demo Platform for testing AndyMark Dual H-Bridge Motor Controller
//CSK Have to keep local modules like DisplayModule.cs because the up-integrated CTRE.Hero.Module.DisplayModule.RectSprite does not have a SetPos method
//CSK This does cause compiler to put squiggly line under DisplayModule references because it sees a local and a reference version.  Local takes precedence
//CSK See email to Jacob Caporuscio <jcaporuscio@ctr-electronics.com> from 1/11/2017 about this.

//CSK 10/23/2019 Redid code for Rover Pneumatic product line
//Logitech F710 (https://www.andymark.com/products/f710-wireless-logitech-game-controller)
//XD switch set to D
//Hold Logitech button until Mode light turns on - if robot on and usb dongle connected the mode light should be solid, if not connected light will flash
//Press mode button to switch to put in flight mode - 
//Controls left joystick forward, reverse and strafe, right joystick is steering

//CSK 10/25/2019
//Uncomment if using the CTRE display module
#define HASDISPLAY
//Uncomment to choose if using all talons or all victors
//#define TALONSRX
#define VICTORSPX

using Microsoft.SPOT;
using Microsoft.SPOT.Hardware;
using System;
using System.Text;
using System.Threading;

using CTRE.Gadgeteer.Module;
using CTRE.Phoenix;
using CTRE.Phoenix.Controller;
using CTRE.Phoenix.MotorControl;
using CTRE.Phoenix.MotorControl.CAN;

namespace RoverMecanumDrive
{
    //CSK 10/11.2016 Creating extension methods in C# https://msdn.microsoft.com/en-us/library/bb383977.aspx
    public static class Extensions
    {
        public static double ClampRange(this double value, double min, double max)
        {
            if (value >= min && value <= max)
                return value;
            else if (value > max)
            {
                return max;
            }
            else if (value < min)
            {
                return min;
            }
            else return 1;
        }
        public static double Deadband(this double value)
        {
            if (value < -0.10)
            {
                /* outside of deadband */
            }
            else if (value > +0.10)
            {
                /* outside of deadband */
            }
            else
            {
                /* within 10% so zero it */
                value = 0;
            }
            return value;
        }

    }

    public class Program
    {
        const double FULL_FORWARD = 1.0;
        const double FULL_REVERSE = -1.0;

#if TALONSRX  //vvvCSK 10/28/2019 Copied from CTRE HeroPixyDrive examplevvv
        /** Talons to control based on their position of the robot (Can be changed if I.D.s are different)*/
        static TalonSRX LeftFront = new TalonSRX(1);
        static TalonSRX LeftRear = new TalonSRX(2);
        static TalonSRX RightFront = new TalonSRX(3);
        static TalonSRX RightRear = new TalonSRX(4);
        static TalonSRX[] Talons = { LeftFront, LeftRear, RightFront, RightRear };
#elif VICTORSPX
        /** Talons to control based on their position of the robot (Can be changed if I.D.s are different)*/
        static VictorSPX LeftFront = new VictorSPX(1);
        static VictorSPX LeftRear = new VictorSPX(2);
        static VictorSPX RightFront = new VictorSPX(3);
        static VictorSPX RightRear = new VictorSPX(4);
        static VictorSPX[] Victors = { LeftFront, LeftRear, RightFront, RightRear };
#endif
        //^^^CSK 10/28/2019 Copied from CTRE HeroPixyDrive example^^^

        static GameController _gamepad = new GameController(UsbHostDevice.GetInstance());

        //CSK 4/7/2017 Kinda like #defines in C 
        //CSK 10/31/2018 Blue button is now 1 instead of 0
        const int BTNBLUEX = 1, BTNGREENA = 2, BTNREDB = 3, BTNYELLOWY = 4, BTNLEFT = 5, BTNRIGHT = 6, TRGRLEFT = 7, TRGRRIGHT = 8;
        const uint DEAD_MAN_BUTTON = 5;  //LB on gamepad
        //CSK 11/30/2018
        const uint LEFT_JOY_X = 0, LEFT_JOY_Y = 1, RIGHT_JOY_X = 2, ANALOGLEFT = 3, ANALOGRIGHT = 4, RIGHT_JOY_Y = 5;

#if (HASDISPLAY)
        //CSK 11/9/2018 Display works on Port1 and Port8
        static DisplayModule _displayModule = new DisplayModule(CTRE.HERO.IO.Port1, DisplayModule.OrientationType.Landscape);

        /* lets pick a font */
        static Font _smallFont = Properties.Resources.GetFont(Properties.Resources.FontResources.small);
        static Font _bigFont = Properties.Resources.GetFont(Properties.Resources.FontResources.NinaB);

        static DisplayModule.ResourceImageSprite _leftCrossHair, _rightCrossHair, _AMLogo; //, _LightBulb;
        static DisplayModule.LabelSprite _labelTitle, _labelBtn, _labelFrontMotors, _labelRearMotors;
        static int lftCrossHairOrigin = 35, rtCrossHairOrigin = 105;
#endif

        public static void Main()
        {
            //vvvCSK 10/28/2019 Copied from CTRE HeroPixyDrive examplevvv
            /* Forword/Backward Scalor */
            const float kScalarX = 0.50f;
            /* Left/Right Scalor */
            const float kScalarY = 0.50f;
            /* Turning Scalor */
            const float kScalarTwist = 0.30f;
            /* Ramp Rate */
            const float kVoltageRampSec = 0.25f;
            //^^^CSK 10/28/2019 Copied from CTRE HeroPixyDrive example^^^

            /* Configure Talons to operate in percentage VBus mode, and Ramp Up Voltage*/
#if TALONSRX
            foreach (TalonSRX temp in Talons)
#else
            foreach (VictorSPX temp in Victors)
#endif
            {
                temp.Set(ControlMode.PercentOutput, 0);
                temp.ConfigOpenloopRamp(kVoltageRampSec);
            }
            //^^^CSK 10/28/2019 Copied from CTRE HeroPixyDrive example^^^

#if (HASDISPLAY)
            _AMLogo = _displayModule.AddResourceImageSprite(Properties.Resources.ResourceManager,
                                                                    Properties.Resources.BinaryResources.andymark_logo_160x26,
                                                                    Bitmap.BitmapImageType.Jpeg,
                                                                    0, 0);

            _labelTitle = _displayModule.AddLabelSprite(_bigFont, DisplayModule.Color.White, 0, 28, 160, 15);

            _labelBtn = _displayModule.AddLabelSprite(_smallFont, DisplayModule.Color.Cyan, 30, 60, 100, 10);

            _leftCrossHair = _displayModule.AddResourceImageSprite(Properties.Resources.ResourceManager,
                                                                   Properties.Resources.BinaryResources.crosshair,
                                                                   Bitmap.BitmapImageType.Jpeg,
                                                                   lftCrossHairOrigin, 100);

            _rightCrossHair = _displayModule.AddResourceImageSprite(Properties.Resources.ResourceManager,
                                                                    Properties.Resources.BinaryResources.crosshair,
                                                                    Bitmap.BitmapImageType.Jpeg,
                                                                    rtCrossHairOrigin, 100);
            //CSK 10/29/2019 This tells the display where to put the motor "speed" info on the screen 
            _labelFrontMotors = _displayModule.AddLabelSprite(_bigFont, DisplayModule.Color.White, 2, 30, 134, 15);
            _labelRearMotors = _displayModule.AddLabelSprite(_bigFont, DisplayModule.Color.White, 7, 45, 128, 15);
#endif

            /* loop forever */
            while (true)
            {
                if (_gamepad.GetConnectionStatus() == CTRE.Phoenix.UsbDeviceConnection.Connected)
                {
                    /* feed watchdog to keep Talon's enabled */
                    CTRE.Phoenix.Watchdog.Feed();
                }
                /* Regular mecanum drive that is scaled and Gamepad joysticks have been adjusted */
                //CSK 10/30/2019 With "standard" AM FRC wiring (M+ to redline terminal with bump) rotation is counter clockwise
                //Based on this wiring signs have been applied to make motion match the joystick control expectations
                float X = -1 * _gamepad.GetAxis(LEFT_JOY_X);
                float Y = _gamepad.GetAxis(LEFT_JOY_Y);
                float Twist = -1 * _gamepad.GetAxis(RIGHT_JOY_X);
                MecanumDrive(Y * kScalarY, X * kScalarX, Twist * kScalarTwist);
            }
        }


        /**
* Mecanum Drive that is inverted on the left side and decreases output when low battery
* 
* @param   Forward  Forward/Backward drive of mecanum drive
* @param   Strafe   Left/Right drive of mecanum drive
* @param   Twist    Turn left/Right of mecanum drive
*/
        private static void MecanumDrive(float Forward, float Strafe, float Twist)
        {
            float leftFrnt = (Forward + Strafe + Twist); /* left front moves positive for forward, strafe-right, turn-right */
            float leftRear = (Forward - Strafe + Twist); /* left rear moves positive for forward, strafe-left, turn-right   */
            float rghtFrnt = (Forward - Strafe - Twist); /* right front moves positive for forward, strafe-left, turn-left  */
            float rghtRear = (Forward + Strafe - Twist); /* right rear moves positive for forward, strafe-right, turn-left  */

            /* Invert left sided motors */
            leftFrnt *= -1;
            leftRear *= -1;

            /* Feed values to Talons */
            RightFront.Set(ControlMode.PercentOutput, rghtFrnt);
            RightRear.Set(ControlMode.PercentOutput, rghtRear);
            LeftFront.Set(ControlMode.PercentOutput, leftFrnt);
            LeftRear.Set(ControlMode.PercentOutput, leftRear);
#if (HASDISPLAY)
            //VVV CSK 11/2/2018 From HeroBridge_with_Arcade_And_Display code
            DisplayData(leftFrnt, leftRear, rghtFrnt, rghtRear);
#endif
            return;
        }
#if (HASDISPLAY)
        //CSK 11/2/2018 From HeroBridge_with_Arcade_And_Display code 
        static void DisplayData(double leftFrnt, double leftRear, double rghtFrnt, double rghtRear)
        {
            _labelFrontMotors.SetText("Front: " + leftFrnt + ", " + rghtFrnt);
            _labelRearMotors.SetText("Rear: " + leftRear + ", " + rghtRear);

            int buttonPressed = GetFirstButton(_gamepad);
            if (buttonPressed < 0)
            {
                _labelBtn.SetColor((DisplayModule.Color)0xA0A0A0); // gray RGB
                _labelBtn.SetText("        No Buttons");
            }
            else
            {
                switch (buttonPressed)
                {
                    case BTNBLUEX: _labelBtn.SetColor(DisplayModule.Color.Blue); break;
                    case BTNGREENA: _labelBtn.SetColor(DisplayModule.Color.Green); break;
                    case BTNREDB: _labelBtn.SetColor(DisplayModule.Color.Red); break;
                    case BTNYELLOWY: _labelBtn.SetColor(DisplayModule.Color.Yellow); break;
                }
                _labelBtn.SetText("Pressed Button " + buttonPressed);
            }

            //CSK 01/11/2016 Display data on CTRE screen
            _leftCrossHair.SetPosition((int)(lftCrossHairOrigin + 15 * _gamepad.GetAxis(LEFT_JOY_X)), 100 + (int)(15 * _gamepad.GetAxis(LEFT_JOY_Y)));
            _rightCrossHair.SetPosition((int)(rtCrossHairOrigin + 15 * _gamepad.GetAxis(RIGHT_JOY_X)), 100 + (int)(15 * _gamepad.GetAxis(RIGHT_JOY_Y)));
            return;
        }
#endif
#if HASDISPLAY
        static int GetFirstButton(GameController gamepad)
        {
            for (uint i = 1; i < 16; ++i)
            {
                if (gamepad.GetButton(i))
                    return (int)i;
            }
            return -1;
        }

    }
#endif
}