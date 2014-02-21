using System;
using System.Linq;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Tomato;
using Tomato.Hardware;
using System.Reflection;

namespace Mir
{
    using System.Collections.Generic;

    using Microsoft.Xna.Framework;

    using Protogame;

    public class MirWorld : IWorld
    {
        private readonly I2DRenderUtilities m_2DRenderUtilities;

        private readonly I3DRenderUtilities m_3DRenderUtilities;

        private readonly IAssetManager m_AssetManager;

        private readonly FontAsset m_DefaultFont;

        private readonly DCPU m_DCPU;

        private readonly LEM1802 m_LEM1802;

        private readonly GenericKeyboard m_GenericKeyboard;

        private readonly EffectAsset m_LightingEffect;

        private RenderTarget2D m_DCPURenderTarget;

        public MirWorld(
            IFactory factory,
            I2DRenderUtilities twoDRenderUtilities,
            I3DRenderUtilities threeDRenderUtilities,
            IAssetManagerProvider assetManagerProvider)
        {
            this.Entities = new List<IEntity>();

            this.m_2DRenderUtilities = twoDRenderUtilities;
            this.m_3DRenderUtilities = threeDRenderUtilities;
            this.m_AssetManager = assetManagerProvider.GetAssetManager();
            this.m_DefaultFont = this.m_AssetManager.Get<FontAsset>("font.Default");
            this.m_LightingEffect = this.m_AssetManager.Get<EffectAsset>("effect.Light");

            this.m_LEM1802 = new LEM1802();
            this.m_GenericKeyboard = new GenericKeyboard();
            this.m_DCPU = new DCPU();
            this.m_DCPU.ConnectDevice(this.m_LEM1802);
            this.m_DCPU.ConnectDevice(this.m_GenericKeyboard);

            var ship = factory.CreateShipEntity();
            var player = factory.CreatePlayerEntity();
            player.ParentArea = ship;

            this.Entities.Add(ship);
            this.Entities.Add(player);

            var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("Mir.test.bin");
            var program = new ushort[stream.Length / 2];
            for (int i = 0; i < program.Length; i++)
            {
                byte left = (byte)stream.ReadByte();
                byte right = (byte)stream.ReadByte();
                ushort value = (ushort)(right | (left << 8));
                program[i] = value;
            }

            this.m_DCPU.FlashMemory(program);
        }

        public IList<IEntity> Entities { get; private set; }

        public void Dispose()
        {
        }

        public void RenderAbove(IGameContext gameContext, IRenderContext renderContext)
        {
            if (renderContext.Is3DContext)
            {
                return;
            }

            this.m_2DRenderUtilities.RenderText(
                renderContext,
                new Vector2(10, 10),
                "Hello Mir!",
                this.m_DefaultFont);

            this.m_2DRenderUtilities.RenderText(
                renderContext,
                new Vector2(10, 30),
                "Running at " + gameContext.FPS + " FPS; " + gameContext.FrameCount + " frames counted so far",
                this.m_DefaultFont);

            this.m_2DRenderUtilities.RenderText(
                renderContext,
                new Vector2(10, 50),
                "Right-click to switch between movement / camera and typing on the DCPU",
                this.m_DefaultFont);
        }

        public void RenderBelow(IGameContext gameContext, IRenderContext renderContext)
        {
            if (!renderContext.Is3DContext)
            {
                return;
            }

            renderContext.GraphicsDevice.Clear(Color.Black);

            var player = this.Entities.OfType<PlayerEntity>().First();

            player.SetCamera(renderContext);

            this.m_3DRenderUtilities.RenderCube(
                renderContext,
                Matrix.CreateTranslation(5, 0, 5),
                Color.Red);

            this.m_3DRenderUtilities.RenderCube(
                renderContext,
                Matrix.CreateTranslation(5, 10, 5),
                Color.Green);

            this.m_3DRenderUtilities.RenderCube(
                renderContext,
                Matrix.CreateTranslation(5, 19, 5),
                Color.Blue);

            // Draw the room.
            /*this.m_3DRenderUtilities.RenderPlane(
                renderContext,
                Matrix.CreateScale(30, 0, 40) *
                Matrix.CreateTranslation(-15, 0, -20),
                Color.Gray);*/

            /*            
wa
            this.m_3DRenderUtilities.RenderPlane(
                renderContext,
                Matrix.CreateScale(30, 0, 40) *
                Matrix.CreateRotationX(MathHelper.Pi) *
                Matrix.CreateTranslation(-15, 20, 20),
                Color.Gray);

            this.m_3DRenderUtilities.RenderPlane(
                renderContext,
                Matrix.CreateScale(20, 0, 40) *
                Matrix.CreateRotationZ(MathHelper.PiOver2) *
                Matrix.CreateTranslation(15, 0, -20),
                Color.DarkGray);

            this.m_3DRenderUtilities.RenderPlane(
                renderContext,
                Matrix.CreateScale(20, 0, 40) *
                Matrix.CreateRotationZ(-MathHelper.PiOver2) *
                Matrix.CreateTranslation(-15, 20, -20),
                Color.DarkGray);

            this.m_3DRenderUtilities.RenderPlane(
                renderContext,
                Matrix.CreateScale(30, 0, 20) *
                Matrix.CreateRotationX(-MathHelper.PiOver2) *
                Matrix.CreateTranslation(-15, 0, 20),
                Color.LightGray);

            this.m_3DRenderUtilities.RenderPlane(
                renderContext,
                Matrix.CreateScale(30, 0, 20) *
                Matrix.CreateRotationX(MathHelper.PiOver2) *
                Matrix.CreateTranslation(-15, 20, -20),
                Color.LightGray);

            */

            // Render DCPU screen.
            this.RenderDCPU(renderContext);

            this.m_3DRenderUtilities.RenderCube(
                renderContext,
                Matrix.CreateScale(2.5f, 2, 2) *
                Matrix.CreateRotationX(MathHelper.Pi) *
                Matrix.CreateRotationY(MathHelper.Pi) *
                Matrix.CreateRotationX(MathHelper.PiOver4) *
                Matrix.CreateTranslation(0, 6f, 19f),
                new TextureAsset(this.m_DCPURenderTarget),
                Vector2.Zero,
                Vector2.One);
        }

        public void Update(IGameContext gameContext, IUpdateContext updateContext)
        {
            this.m_DCPU.Execute((int)(100000 * gameContext.GameTime.ElapsedGameTime.TotalSeconds));

            var player = this.Entities.OfType<PlayerEntity>().First();
            if (player.CaptureMouse)
            {
                return;
            }

            var keyboard = Keyboard.GetState();
            var values = Enum.GetValues(typeof(Keys));
            var winNames = Enum.GetNames(typeof(System.Windows.Forms.Keys));
            foreach (var id in values)
            {
                var name = Enum.GetName(typeof(Keys), id);
                if (!winNames.Contains(name))
                {
                    if (name == "LeftShift" || name == "RightShift")
                    {
                        name = "Shift";
                    }
                    else if (name == "LeftControl" || name == "RightControl")
                    {
                        name = "Control";
                    }
                    else
                    {
                        continue;
                    }
                }

                if (keyboard.IsKeyPressed(this, (Keys)id))
                {
                    this.m_GenericKeyboard.KeyDown((System.Windows.Forms.Keys)Enum.Parse(typeof(System.Windows.Forms.Keys), name));
                }

                if (keyboard.IsKeyUp((Keys)id))
                {
                    this.m_GenericKeyboard.KeyUp((System.Windows.Forms.Keys)Enum.Parse(typeof(System.Windows.Forms.Keys), name));
                }
            }
        }

        private void RenderDCPU(IRenderContext renderContext)
        {
            if (this.m_DCPURenderTarget == null)
            {
                this.m_DCPURenderTarget = new RenderTarget2D(
                    renderContext.GraphicsDevice,
                    LEM1802.Width,
                    LEM1802.Height);
            }

            // Copied from LEM1802.
            if (this.m_LEM1802.ScreenMap == 0)
            {
                return;
            }

            Color[] pixels = new Color[LEM1802.Width * LEM1802.Height];

            ushort address = 0;
            for (int y = 0; y < 12; y++)
                for (int x = 0; x < 32; x++)
                {
                    ushort value = this.m_LEM1802.AttachedCPU.Memory[this.m_LEM1802.ScreenMap + address];
                    uint fontValue;
                    if (this.m_LEM1802.FontMap == 0)
                        fontValue = (uint)((LEM1802.DefaultFont[(value & 0x7F) * 2] << 16) | LEM1802.DefaultFont[(value & 0x7F) * 2 + 1]);
                    else
                        fontValue = (uint)((this.m_LEM1802.AttachedCPU.Memory[this.m_LEM1802.FontMap + ((value & 0x7F) * 2)] << 16) | this.m_LEM1802.AttachedCPU.Memory[this.m_LEM1802.FontMap + ((value & 0x7F) * 2) + 1]);
                    fontValue = BitConverter.ToUInt32(BitConverter.GetBytes(fontValue).Reverse().ToArray(), 0);

                    var backgroundc = this.m_LEM1802.GetPaletteColor((byte)((value & 0xF00) >> 8));
                    var foregroundc = this.m_LEM1802.GetPaletteColor((byte)((value & 0xF000) >> 12));
                    var background = new Color(backgroundc.R, backgroundc.G, backgroundc.B, backgroundc.A);
                    var foreground = new Color(foregroundc.R, foregroundc.G, foregroundc.B, foregroundc.A);
                    for (int i = 0; i < sizeof(uint) * 8; i++)
                    {
                        var px = i / 8 + (x * LEM1802.CharWidth);
                        var py = i % 8 + (y * LEM1802.CharHeight);
                        if ((fontValue & 1) == 0 || (((value & 0x80) == 0x80) && !this.m_LEM1802.BlinkOn))
                        {
                            pixels[px + py * LEM1802.Width] = background;
                        }
                        else
                        {
                            pixels[px + py * LEM1802.Width] = foreground;
                        }
                        fontValue >>= 1;
                    }
                    address++;
                }

            this.m_DCPURenderTarget.SetData(pixels);
        }
    }
}
