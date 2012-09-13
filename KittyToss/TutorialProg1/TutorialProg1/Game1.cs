using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Audio;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.GamerServices;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Microsoft.Xna.Framework.Media;
using Microsoft.Kinect;

namespace TutorialProg1
{
    /// <summary>
    /// This is the main type for your game
    /// </summary>
    public class Game1 : Microsoft.Xna.Framework.Game
    {
        GraphicsDeviceManager graphics;
        SpriteBatch spriteBatch;
        KinectSensor kinectSensor;
       
        Texture2D kinectRGBVideoCap;
        Texture2D overlay;
        Texture2D rightHandPicture;

        Vector2 newRightHandPosition;
        Vector2 oldRightHandPosition;
        Vector2 newRightHandVelocity;
        Vector2 oldRightHandVelocity;
        Vector2 objectPosition;
        Vector2 vecMaxVelocity;
        TimeSpan prevTime;

        SpriteFont font;

        string connectedStatus;
        string velocityOutput;
        const int screenHeight = 480;
        const int screenWidth = 640;
        int numFramesAbove;
        const int numFramesThreshold = 4;
        const int pixelVelocityThreshold = 150;
        const float stoppingPixelVelocityThreshold = .40f;
        bool tracking;
        int maxVelocity;
        int velocityMagnitude;
        bool objectThrowing;

        public Game1()
        {
            graphics = new GraphicsDeviceManager(this);
            Content.RootDirectory = "Content";

            graphics.PreferredBackBufferWidth = screenWidth;
            graphics.PreferredBackBufferHeight = screenHeight;

            newRightHandPosition = new Vector2(0, 0);
            oldRightHandPosition = new Vector2(0, 0);
            newRightHandVelocity = new Vector2(0, 0);
            oldRightHandVelocity = new Vector2(0, 0);

            //IsFixedTimeStep = false;

            velocityOutput = "";
            tracking = false;
            objectThrowing = false;
            prevTime = new TimeSpan(0, 0, 0);
        }

        private void DiscoverKinectSensor()
        {
            foreach (KinectSensor sensor in KinectSensor.KinectSensors)
            {
                if (sensor.Status == KinectStatus.Connected)
                {
                    kinectSensor = sensor;
                    break;
                }
            }

            if (this.kinectSensor == null)
            {
                connectedStatus = "No kinect is kinect-ed!!";
                return;
            }

            switch (kinectSensor.Status)
            {
                case KinectStatus.Connected:
                    {
                        connectedStatus = "Status: Kinect-ed yo!";
                        InitializeKinect();

                        kinectSensor.ElevationAngle = 15;

                        break;
                    }

                case KinectStatus.Disconnected:
                    {
                        connectedStatus = "Status: Diskinect-ed";
                        break;
                    }

                case KinectStatus.NotPowered:
                    {
                        connectedStatus = "Status: Where's the power?";
                        break;
                    }

                default:
                    {
                        connectedStatus = "Status: Something is up!";
                        break;
                    }
            }
        }

        /// <summary>
        /// Allows the game to perform any initialization it needs to before starting to run.
        /// This is where it can query for any required services and load any non-graphic
        /// related content.  Calling base.Initialize will enumerate through any components
        /// and initialize them as well.
        /// </summary>
        protected override void Initialize()
        {
            KinectSensor.KinectSensors.StatusChanged += new EventHandler<StatusChangedEventArgs>(KinectSensors_StatusChanged);
            DiscoverKinectSensor();

            base.Initialize();
        }

        private bool InitializeKinect()
        {
            kinectSensor.ColorStream.Enable(ColorImageFormat.RgbResolution640x480Fps30);
            kinectSensor.ColorFrameReady += new EventHandler<ColorImageFrameReadyEventArgs>(kinectSensor_ColorFrameReady);
            //Skeleton stream
            kinectSensor.SkeletonStream.Enable(new TransformSmoothParameters()
            {
                Smoothing = 0.5f,
                Correction = 0.5f,
                Prediction = 0.5f,
                JitterRadius = 0.5f,
                MaxDeviationRadius = 0.04f
            });

            kinectSensor.SkeletonFrameReady += new EventHandler<SkeletonFrameReadyEventArgs>(kinectSensor_SkeletonFrameReady);

            try
            {
                kinectSensor.Start();
            }
            catch
            {
                connectedStatus = "Could start up the Kinect Sensor!!";
                return false;
            }

            return true;
        }

        void kinectSensor_ColorFrameReady(object sender, ColorImageFrameReadyEventArgs e)
        {
            using (ColorImageFrame colorImageFrame = e.OpenColorImageFrame())
            {
                if (colorImageFrame != null)
                {
                    byte[] pixelsFromFrame = new byte[colorImageFrame.PixelDataLength];

                    colorImageFrame.CopyPixelDataTo(pixelsFromFrame);

                    Color[] color = new Color[colorImageFrame.Height * colorImageFrame.Width];
                    kinectRGBVideoCap = new Texture2D(graphics.GraphicsDevice, colorImageFrame.Width, colorImageFrame.Height);

                    //Going through each pixel and setting the bytes correctly.
                    //Each pixel has a Red, Green, and Blue channel
                    int index = 0;
                    for (int y = 0; y < colorImageFrame.Height; y++)
                    {
                        for (int x = 0; x < colorImageFrame.Width; x++, index += 4)
                        {
                            //RGB
                            color[y * colorImageFrame.Width + x] = new Color(pixelsFromFrame[index + 2], pixelsFromFrame[index + 1], pixelsFromFrame[index+0]);
                        }
                    }

                    kinectRGBVideoCap.SetData(color);
                }
            }
        }

        void kinectSensor_SkeletonFrameReady(object sender, SkeletonFrameReadyEventArgs e)
        {
            
            using (SkeletonFrame skeletonFrame = e.OpenSkeletonFrame())
            {
                if (skeletonFrame != null)
                {
                    Skeleton[] skeletonData = new Skeleton[skeletonFrame.SkeletonArrayLength];

                    skeletonFrame.CopySkeletonDataTo(skeletonData);
                    Skeleton playerSkeleton = (from s in skeletonData where s.TrackingState == SkeletonTrackingState.Tracked select s).FirstOrDefault();

                    if (playerSkeleton != null && !objectThrowing)
                    {
                        Joint rightHandJoint = playerSkeleton.Joints[JointType.HandRight];

                        //Set the old right hand to the "new" to save it, then get the new right hand position
                        oldRightHandPosition = newRightHandPosition;
                        newRightHandPosition = new Vector2((((0.5f * rightHandJoint.Position.X) + 0.5f) * (640)), (((-0.5f * rightHandJoint.Position.Y) + 0.5f) * (480)));

                        //Velocity in pixels / second
                        oldRightHandVelocity = newRightHandVelocity;
                        newRightHandVelocity = (newRightHandPosition - oldRightHandPosition) / (1 / 30f);
                        velocityMagnitude = (int) Math.Sqrt(newRightHandVelocity.X * newRightHandVelocity.X + newRightHandVelocity.Y * newRightHandVelocity.Y);


                        if (velocityMagnitude > pixelVelocityThreshold)
                        {
                            numFramesAbove++;

                            if (numFramesAbove > numFramesThreshold)
                            {
                                if (velocityMagnitude > maxVelocity)
                                {
                                    maxVelocity = velocityMagnitude;
                                    vecMaxVelocity = newRightHandVelocity;
                                   // vecMaxVelocity.Y = vecMaxVelocity.Y * -1;
                                }

                                if (velocityMagnitude > maxVelocity * stoppingPixelVelocityThreshold)
                                {
                                    tracking = true;
                                }
                                else
                                {
                                    if (!objectThrowing)
                                    {
                                        objectThrowing = true;
                                        objectPosition = newRightHandPosition;
                                    }
                                }
                            }
                        }

                        else
                        {
                            numFramesAbove = 0;
                            tracking = false;
                        }
                    }
                }
            }
        }

        void KinectSensors_StatusChanged(object sender, StatusChangedEventArgs e)
        {
            if (this.kinectSensor == e.Sensor)
            {
                if (e.Status == KinectStatus.Disconnected ||
                    e.Status == KinectStatus.NotPowered)
                {
                    this.kinectSensor = null;
                    this.DiscoverKinectSensor();
                }
            }
        }

        /// <summary>
        /// LoadContent will be called once per game and is the place to load
        /// all of your content.
        /// </summary>
        protected override void LoadContent()
        {
            // Create a new SpriteBatch, which can be used to draw textures.
            spriteBatch = new SpriteBatch(GraphicsDevice);

            kinectRGBVideoCap = new Texture2D(GraphicsDevice, 1377, 1377);

            overlay = Content.Load<Texture2D>("overlay");
            font = Content.Load<SpriteFont>("SpriteFont1");
            rightHandPicture = Content.Load<Texture2D>("rightCat");

            // TODO: use this.Content to load your game content here
        }

        /// <summary>
        /// UnloadContent will be called once per game and is the place to unload
        /// all content.
        /// </summary>
        protected override void UnloadContent()
        {
            // TODO: Unload any non ContentManager content here
            kinectSensor.Stop();
            kinectSensor.Dispose();
        }

        /// <summary>
        /// Allows the game to run logic such as updating the world,
        /// checking for collisions, gathering input, and playing audio.
        /// </summary>
        /// <param name="gameTime">Provides a snapshot of timing values.</param>
        protected override void Update(GameTime gameTime)
        {
            // Allows the game to exit
            if (GamePad.GetState(PlayerIndex.One).Buttons.Back == ButtonState.Pressed)
                this.Exit();

            // TODO: Add your update logic here



            base.Update(gameTime);
        }

        /// <summary>
        /// This is called when the game should draw itself.
        /// </summary>
        /// <param name="gameTime">Provides a snapshot of timing values.</param>
        protected override void Draw(GameTime gameTime)
        {
            GraphicsDevice.Clear(Color.CornflowerBlue);
            // TODO: Add your drawing code here
            float catScale = 0.5f;
            Vector2 adjustedRightHandPosition = new Vector2(newRightHandPosition.X - rightHandPicture.Bounds.Width * catScale, newRightHandPosition.Y - rightHandPicture.Bounds.Height * catScale);
            spriteBatch.Begin();
            spriteBatch.Draw(kinectRGBVideoCap, new Rectangle(0, 0, 640, 480), Color.White);
            TimeSpan timeDiff = gameTime.TotalGameTime - prevTime;
            prevTime = gameTime.TotalGameTime;

            if (objectThrowing)
            {
                objectPosition = objectPosition + newRightHandVelocity * ((timeDiff.Milliseconds / 1000f) + timeDiff.Seconds);

                if (objectPosition.X < 0 || objectPosition.X > screenWidth || objectPosition.Y < 0 || objectPosition.Y > screenHeight)
                {
                    objectThrowing = false;
                    maxVelocity = -20000;
                }
            }

            if (!objectThrowing)
            {
                if (adjustedRightHandPosition.X > 0 && adjustedRightHandPosition.X < screenWidth && adjustedRightHandPosition.Y > 0 && adjustedRightHandPosition.Y < screenHeight)
                    spriteBatch.Draw(rightHandPicture, adjustedRightHandPosition, null, Color.White, 0f, Vector2.Zero, catScale, SpriteEffects.None, 0);
            }

            else
            {
                spriteBatch.Draw(rightHandPicture, objectPosition, null, Color.White, 0f, Vector2.Zero, catScale, SpriteEffects.None, 0);
            }
            
            spriteBatch.DrawString(font, tracking.ToString(), new Vector2(20, 80), Color.White);
            spriteBatch.End();

            base.Draw(gameTime);
        }
    }
}
