﻿using SharpDX;
using SharpDX.DXGI;
using SharpDX.Multimedia;
using SharpDX.Toolkit;
using SharpDX.Toolkit.Graphics;
using SharpDX.X3DAudio;
using SharpDX.XAudio2;
using System;
using System.Collections.Generic;
using System.Linq;
using VirtualSpace.Core;
using VirtualSpace.Core.Math;
using VirtualSpace.Core.Renderer.Screen;
using VirtualSpace.Platform.Windows.Rendering.Providers;
using VirtualSpace.Platform.Windows.Video;

namespace VirtualSpace.Platform.Windows.Rendering.Screen
{
    internal sealed class ScreenRenderer : GameSystem, IScreen
    {
        private readonly ICameraProvider _cameraService;
        private readonly IScreenSource _source;
        private readonly Dictionary<Speakers, SpeakerOutput> _speakerOutputs;

        private BasicEffect _basicEffect;
        private Vector3 _position;
        private Vector3 _lookAt;
        private bool _posDirty;

        private GeometricPrimitive _plane;
        private SharpDX.Direct3D11.ShaderResourceView _planeShaderView;
        private Matrix _scale;
        private KeyedMutex _renderMutex;

        private X3DAudio _x3DAudio;
        private Listener _audioListener;
        private DspSettings _dspSettings;
        private bool _stereoVirtualisation;
        private bool _enableStereoDelay;
        private float[] _lowFreqOutput;

        public ScreenRenderer(Game game, ICameraProvider camera, IScreenSource source, float screenSize, float curveRadius)
            : base(game)
        {
            _source = source;
            _cameraService = camera;
            _speakerOutputs = new Dictionary<Speakers, SpeakerOutput>();
            _enableStereoDelay = true;
            _posDirty = true;
            _position = Vector3.Zero;
            _lookAt = Vector3.ForwardRH;

            Visible = true;
            Enabled = true;
            ScreenSize = screenSize;
            CurveRadius = curveRadius;

            DrawOrder = UpdateOrder = RenderingOrder.World;

            game.GameSystems.Add(this);
            ToDispose(new Disposable(() =>
            {
                ((IContentable)this).UnloadContent();
                game.GameSystems.Remove(this);
            }));
        }

        protected override void LoadContent()
        {
            base.LoadContent();

            var output = _source.GetOutput(GraphicsDevice);
            
            /*************************************************
             * Setup Video
             * *******************************************/
            var desc = output.Texture.Description;

            // Render mutex is optional (depends on the returned texture)
            if ((desc.OptionFlags & SharpDX.Direct3D11.ResourceOptionFlags.SharedKeyedmutex) == SharpDX.Direct3D11.ResourceOptionFlags.SharedKeyedmutex)
            {
                _renderMutex = ToDisposeContent(output.Texture.QueryInterface<KeyedMutex>());
            }
            else
            {
                _renderMutex = null;
            }

            _basicEffect = ToDisposeContent(new BasicEffect(GraphicsDevice));
            _basicEffect.TextureEnabled = true;
            _basicEffect.LightingEnabled = false;

            _planeShaderView = ToDisposeContent(new SharpDX.Direct3D11.ShaderResourceView(GraphicsDevice, output.Texture, new SharpDX.Direct3D11.ShaderResourceViewDescription
            {
                Format = desc.Format,
                Dimension = SharpDX.Direct3D.ShaderResourceViewDimension.Texture2D,
                Texture2D = new SharpDX.Direct3D11.ShaderResourceViewDescription.Texture2DResource { MipLevels = desc.MipLevels, MostDetailedMip = desc.MipLevels - 1 }
            }));

            if (CurveRadius <= 0.01 || CurveRadius > 100000)
            {
                _plane = ToDisposeContent(GeometricPrimitive.Plane.New(GraphicsDevice, desc.Width, desc.Height));
            }
            else
            {
                _plane = ToDisposeContent(CreateCurvedSurface(GraphicsDevice, CurveRadius * desc.Width / ScreenSize, desc.Width, desc.Height, 100));
            }

            var screenWidth = (float)(ScreenSize * Math.Cos(Math.Atan2(desc.Height, desc.Width)));
            _scale = Matrix.Scaling(screenWidth / (float)desc.Width);
            _basicEffect.TextureView = _planeShaderView;
            _posDirty = true;

            /********************************************
             * SETUP AUDIO
             * *****************************************/
            if(output.Audio != null)
            {
                var distanceBase = screenWidth / 3f;
                output.Audio.SetVolume((float)Math.Pow(2, distanceBase) * 4);

                _x3DAudio = MediaAndDeviceManager.Current.X3DAudioEngine;
                var outputChannels = MediaAndDeviceManager.Current.MasterVoice.VoiceDetails.InputChannelCount;
                if(outputChannels != 2 && outputChannels != 6)
                {
                    throw new InvalidOperationException("Only support Stereo or 5.1 output sound currently");
                }
                _stereoVirtualisation = outputChannels == 2;

                var sampleRate = output.Audio.VoiceDetails.InputSampleRate;

                var sourceAudioChannels = output.Audio.VoiceDetails.InputChannelCount;
                if (sourceAudioChannels != 1 && sourceAudioChannels != 2 && sourceAudioChannels != 6)
                {
                    throw new InvalidOperationException("Only support Mono, Stereo, 5.1 input sound currently");
                }

                _speakerOutputs.Add(Speakers.FrontLeft, CreateSpeakerOutput(sampleRate, distanceBase, outputChannels));
                _speakerOutputs.Add(Speakers.FrontRight, CreateSpeakerOutput(sampleRate, distanceBase, outputChannels));

                if (sourceAudioChannels >= 6)
                {
                    _speakerOutputs.Add(Speakers.FrontCenter, CreateSpeakerOutput(sampleRate, distanceBase, outputChannels));
                    _speakerOutputs.Add(Speakers.BackLeft, CreateSpeakerOutput(sampleRate, distanceBase, outputChannels));
                    _speakerOutputs.Add(Speakers.BackRight, CreateSpeakerOutput(sampleRate, distanceBase, outputChannels));

                    // Ignore LF if we only have headphones...
                    if (!_stereoVirtualisation)
                    {
                        _speakerOutputs.Add(Speakers.LowFrequency, CreateSpeakerOutput(sampleRate, distanceBase, outputChannels));
                    }
                }

                output.Audio.SetOutputVoices(_speakerOutputs.Values.Select(v => new VoiceSendDescriptor(v.Voice)).ToArray());

                foreach(var o in _speakerOutputs)
                {
                    var levelMatrix = new float[sourceAudioChannels];
                    switch(o.Key)
                    {
                        case Speakers.FrontLeft:
                            levelMatrix[0] = 1.0f;
                            break;
                        case Speakers.FrontRight:
                            levelMatrix[sourceAudioChannels == 1 ? 0 : 1] = 1.0f; // Mono sound comes out of both channels
                            break;
                        case Speakers.FrontCenter:
                            levelMatrix[2] = 1.0f;
                            break;
                        case Speakers.LowFrequency:
                            levelMatrix[3] = 1.0f;
                            break;
                        case Speakers.BackLeft:
                            levelMatrix[4] = 1.0f;
                            break;
                        case Speakers.BackRight:
                            levelMatrix[5] = 1.0f;
                            break;
                        default:
                            throw new InvalidOperationException("Bad speaker output configuration");
                    }
                    output.Audio.SetOutputMatrix(o.Value.Voice, sourceAudioChannels, 1, levelMatrix);
                }

                _dspSettings = new DspSettings(1, outputChannels);
                _audioListener = new Listener
                {
                    OrientFront = new Vector3(0, 0, 1),
                    OrientTop = new Vector3(0, 1, 0),
                    Position = new Vector3(0, 0, 0),
                    Velocity = new Vector3(0, 0, 0)
                };

                if(!_stereoVirtualisation)
                {
                    _lowFreqOutput = new float[6] { 0, 0, 0, 1f, 0, 0 };
                }
            }
        }

        protected override void UnloadContent()
        {
            base.UnloadContent();

            _speakerOutputs.Clear();
        }

        public override void Update(GameTime gameTime)
        {
            base.Update(gameTime);

            if(_posDirty)
            {
                var rot = Matrix.LookAtRH(Vector3.Zero, _lookAt - _position, Vector3.Up);
                rot.Transpose();
                var mat = _scale * rot * Matrix.Translation(_position);
                Vector3 v1, v2;
                Quaternion q2;
                mat.Decompose(out v1, out q2, out v2);
                _basicEffect.World = mat;
                _posDirty = false;
            }

            _source.Update(gameTime);

            if (_speakerOutputs.Count > 0)
            {
                // For 3D calcs for audio... easier to use model space
                _audioListener.Position = Matrix.Invert(_cameraService.View).TranslationVector;
                _audioListener.OrientFront = _cameraService.View.Forward;
                _audioListener.OrientTop = _cameraService.View.Up;

                foreach (var o in _speakerOutputs)
                {
                    if (_stereoVirtualisation)
                    {
                        // Calculate X3DAudio settings
                        _x3DAudio.Calculate(_audioListener, o.Value.Emitter, _enableStereoDelay ? CalculateFlags.Matrix | CalculateFlags.Delay : CalculateFlags.Matrix, _dspSettings);

                        // Modify XAudio2 source voice settings
                        o.Value.Voice.SetOutputMatrix(1, 2, _dspSettings.MatrixCoefficients);

                        // TODO: Reenable when 2.6.3 SharpDX comes out with fixes I added
                        //if (_enableStereoDelay)
                        //{
                        //    var baseDelay = _dspSettings.EmitterToListenerDistance / X3DAudio.SpeedOfSound * 1000;
                        //    var leftDelay = baseDelay + _dspSettings.DelayTimes[0];
                        //    var rightDelay = baseDelay + _dspSettings.DelayTimes[1];
                        //    o.Value.Voice.SetEffectParameters<AudioDelayParam>(0, new AudioDelayParam { LeftDelay = leftDelay, RightDelay = rightDelay });
                        //}
                    }
                    else
                    {
                        if (o.Key == Speakers.LowFrequency)
                        {
                            // Calculate X3DAudio settings
                            _x3DAudio.Calculate(_audioListener, o.Value.Emitter, CalculateFlags.Matrix | CalculateFlags.RedirectToLfe, _dspSettings);

                            _lowFreqOutput[3] = _dspSettings.MatrixCoefficients[3];
                            o.Value.Voice.SetOutputMatrix(1, 6, _lowFreqOutput);
                        }
                        else
                        {
                            // Calculate X3DAudio settings
                            _x3DAudio.Calculate(_audioListener, o.Value.Emitter, CalculateFlags.Matrix, _dspSettings);

                            // Modify XAudio2 source voice settings
                            o.Value.Voice.SetOutputMatrix(1, 6, _dspSettings.MatrixCoefficients);
                        }
                    }
                }
            }
        }

        public override void Draw(GameTime gameTime)
        {
            base.Draw(gameTime);

            _basicEffect.View = _cameraService.View;
            _basicEffect.Projection = _cameraService.Projection;

            if (_renderMutex == null)
            {
                _plane.Draw(_basicEffect);
            }
            else
            {
                // While drawing make sure we have exlusive access to memory
                var result = _renderMutex.Acquire(0, 100);
                if (result != Result.WaitTimeout && result != Result.Ok)
                {
                    throw new SharpDXException(result);
                }

                if (result == Result.Ok)
                {
                    _plane.Draw(_basicEffect);

                    _renderMutex.Release(0);
                }
            }
        }

        public float ScreenSize { get; set; }
        public float CurveRadius { get; set; }
        public bool HasStereoDelay { get { return _stereoVirtualisation; } }
        public bool StereoDelayEnabled
        {
            get { return _enableStereoDelay; }
            set
            {
                _enableStereoDelay = value;
                // TODO: Reenable when 2.6.3 SharpDX comes out with fixes I added
                //foreach (var o in _speakerOutputs.Where(s => s.Key != Speakers.LowFrequency))
                //{
                //    if (value)
                //    {
                //        o.Value.Voice.EnableEffect(0);
                //    }
                //    else
                //    {
                //        o.Value.Voice.DisableEffect(0);
                //    }
                //}
            }
        }

        public void SetPosition(Vec3 pos)
        {
            _position.X = pos.X;
            _position.Y = pos.Y;
            _position.Z = pos.Z;
            _posDirty = true;
        }

        public void SetFacing(Vec3 pos)
        {
            _lookAt.X = pos.X;
            _lookAt.Y = pos.Y;
            _lookAt.Z = pos.Z;
            _posDirty = true;
        }

        private SpeakerOutput CreateSpeakerOutput(int sampleRate, float minDistance, int outputChannels)
        {
            // TODO: Reenable when 2.6.3 SharpDX comes out with fixes I added
            //var desc = new EffectDescriptor(new AudioDelayEffect(1000), outputChannels);
            var voice = new SubmixVoice(MediaAndDeviceManager.Current.AudioEngine, 1, sampleRate, SubmixVoiceFlags.None, 10 /*, desc*/);
            ToDisposeContent(new Disposable(() => { voice.DestroyVoice(); voice.Dispose(); }));
            //voice.SetEffectParameters<AudioDelayParam>(0, new AudioDelayParam { Delays = new float[outputChannels] });
            //if (!_enableStereoDelay)
            //{
            //    voice.DisableEffect(0);
            //}

            voice.SetVolume((float)(1.0 / Math.Pow(2, minDistance)));

            return new SpeakerOutput
            {
                Voice = voice,
                Emitter = new Emitter
                {
                    ChannelCount = 1,
                    CurveDistanceScaler = minDistance,
                    OrientFront = new Vector3(0, 0, 1),
                    OrientTop = new Vector3(0, 1, 0),
                    Position = new Vector3(0, 0, 0),
                    Velocity = new Vector3(0, 0, 0)
                }
            };
        }

        private static GeometricPrimitive CreateCurvedSurface(GraphicsDevice device, float distance, float width, float height, int tessellation)
        {
            if (tessellation < 1)
            {
                throw new ArgumentOutOfRangeException("tessellation", "tessellation must be > 0");
            }

            // Setup memory
            var vertices = new VertexPositionNormalTexture[tessellation * 2 + 2];
            var indices = new int[tessellation * 6];

            UpdateCurvedVectors(vertices, distance, width, height);

            var currentIndex = 0;
            for (var i = 0; i < tessellation; i++)
            {
                var iBase = i * 2;
                indices[currentIndex++] = iBase;
                indices[currentIndex++] = iBase + 3;
                indices[currentIndex++] = iBase + 1;

                indices[currentIndex++] = iBase;
                indices[currentIndex++] = iBase + 2;
                indices[currentIndex++] = iBase + 3;
            }

            return new GeometricPrimitive(device, vertices, indices) { Name = "Half cylinder" }; 
        }

        private static void UpdateCurvedVectors(VertexPositionNormalTexture[] vertices, float distance, float width, float height)
        {
            var tessellation = vertices.Length / 2 - 1;
            var invTes = 1.0f / tessellation;

            var totalAngle = width / distance;
            var deltaAngle = totalAngle * invTes;
            var sizeY = height / 2;
            var startAngle = totalAngle / 2;

            var currentVertex = 0;

            for (var i = 0; i <= tessellation; i++)
            {
                var currentAngle = startAngle - deltaAngle * i;

                // Top coord
                var x = distance * (float)Math.Sin(currentAngle);
                var z = distance * ((float)Math.Cos(currentAngle) - 1); // Will be negative, means towards the user in RHS
                var position = new Vector3(x, sizeY, z);
                var normal = new Vector3(-x, 0, z); // shared normal for both points
                normal.Normalize();
                var textCoord = new Vector2(i * invTes, 0);
                vertices[currentVertex++] = new VertexPositionNormalTexture(position, normal, textCoord);

                // Bottom coord
                position = new Vector3(x, -sizeY, z);
                textCoord = new Vector2(i * invTes, 1);
                vertices[currentVertex++] = new VertexPositionNormalTexture(position, normal, textCoord);
            }
        }

        private sealed class SpeakerOutput
        {
            public SubmixVoice Voice { get; set; }
            public Emitter Emitter { get; set; }
        }
    }
}
