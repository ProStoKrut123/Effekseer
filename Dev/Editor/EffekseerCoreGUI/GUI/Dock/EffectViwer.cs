using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Effekseer.swig;

namespace Effekseer.GUI.Dock
{
	public class EffectViwerPaneBase : DockPanel
	{
		public enum MoveGizmoAxis
		{
			None = 0,
			X = 1,
			Y = 2,
			Z = 3,
		}

		public struct MoveGizmoRenderInfo
		{
			public float CenterX;
			public float CenterY;
			public float CenterZ;
			public float AxisLength;
			public MoveGizmoAxis HoveredAxis;
			public MoveGizmoAxis ActiveAxis;
		}

		struct MathVector2
		{
			public float X;
			public float Y;

			public MathVector2(float x, float y)
			{
				X = x;
				Y = y;
			}

			public float Length()
			{
				return (float)Math.Sqrt(X * X + Y * Y);
			}

			public float LengthSquared()
			{
				return X * X + Y * Y;
			}

			public static float Dot(MathVector2 lhs, MathVector2 rhs)
			{
				return lhs.X * rhs.X + lhs.Y * rhs.Y;
			}

			public static MathVector2 operator +(MathVector2 lhs, MathVector2 rhs)
			{
				return new MathVector2(lhs.X + rhs.X, lhs.Y + rhs.Y);
			}

			public static MathVector2 operator -(MathVector2 lhs, MathVector2 rhs)
			{
				return new MathVector2(lhs.X - rhs.X, lhs.Y - rhs.Y);
			}

			public static MathVector2 operator *(MathVector2 lhs, float rhs)
			{
				return new MathVector2(lhs.X * rhs, lhs.Y * rhs);
			}
		}

		struct MathVector3
		{
			public float X;
			public float Y;
			public float Z;

			public MathVector3(float x, float y, float z)
			{
				X = x;
				Y = y;
				Z = z;
			}

			public static MathVector3 operator +(MathVector3 lhs, MathVector3 rhs)
			{
				return new MathVector3(lhs.X + rhs.X, lhs.Y + rhs.Y, lhs.Z + rhs.Z);
			}

			public static MathVector3 operator -(MathVector3 lhs, MathVector3 rhs)
			{
				return new MathVector3(lhs.X - rhs.X, lhs.Y - rhs.Y, lhs.Z - rhs.Z);
			}

			public static MathVector3 operator *(MathVector3 lhs, float rhs)
			{
				return new MathVector3(lhs.X * rhs, lhs.Y * rhs, lhs.Z * rhs);
			}

			public static float Dot(MathVector3 lhs, MathVector3 rhs)
			{
				return lhs.X * rhs.X + lhs.Y * rhs.Y + lhs.Z * rhs.Z;
			}

			public static MathVector3 Cross(MathVector3 lhs, MathVector3 rhs)
			{
				return new MathVector3(
					lhs.Y * rhs.Z - lhs.Z * rhs.Y,
					lhs.Z * rhs.X - lhs.X * rhs.Z,
					lhs.X * rhs.Y - lhs.Y * rhs.X);
			}

			public float Length()
			{
				return (float)Math.Sqrt(X * X + Y * Y + Z * Z);
			}

			public MathVector3 Normalized()
			{
				var length = Length();
				if (length < 1e-6f)
				{
					return new MathVector3(0.0f, 0.0f, 0.0f);
				}

				return new MathVector3(X / length, Y / length, Z / length);
			}
		}

		struct MathRay
		{
			public MathVector3 Origin;
			public MathVector3 Direction;
		}

		struct CameraBasis
		{
			public MathVector3 CameraOrigin;
			public MathVector3 Forward;
			public MathVector3 Right;
			public MathVector3 Up;
		}

		const float PerspectiveFovRad = 60.0f / 180.0f * 3.14159265f;
		const float DistanceBase = 15.0f;
		const float OrthoScaleBase = 16.0f;
		const float MinimumAxisLength = 0.25f;

		public bool IsHovered = false;

		float viewportRectMinX = 0.0f;
		float viewportRectMinY = 0.0f;
		float viewportRectSizeX = 0.0f;
		float viewportRectSizeY = 0.0f;
		bool hasViewportRect = false;

		bool isMoveInViewportModeEnabled = false;
		bool isMoveGizmoDragging = false;
		bool isMoveCommandCollectionStarted = false;

		Data.Node dragTargetNode = null;
		MathVector3 dragStartPosition = new MathVector3(0.0f, 0.0f, 0.0f);
		float dragStartAxisParameter = 0.0f;

		MoveGizmoAxis hoveredMoveAxis = MoveGizmoAxis.None;
		MoveGizmoAxis activeMoveAxis = MoveGizmoAxis.None;

		protected Component.Enum renderMode;
		protected Component.Enum viewMode;

		swig.DeviceType deviceType;

		public EffectViwerPaneBase(swig.DeviceType deviceType)
		{
			Label = Resources.GetString("Viewer") + "###Viewer";
			renderMode = new Component.Enum();
			renderMode.Initialize(typeof(Data.OptionValues.RenderMode));
			renderMode.SetBinding(Core.Option.RenderingMode);
			renderMode.EnableUndo = false;
			viewMode = new Component.Enum();
			viewMode.Initialize(typeof(Data.OptionValues.ViewMode));
			viewMode.SetBinding(Core.Option.ViewerMode);
			viewMode.EnableUndo = false;

			NoPadding = true;
			NoScrollBar = true;
			NoCloseButton = true;
			AllowsShortTab = false;

			this.deviceType = deviceType;
		}

		public bool IsMoveInViewportModeEnabled
		{
			get { return isMoveInViewportModeEnabled; }
		}

		public bool CanUseMoveInViewport(out string reason)
		{
			Data.Node node;
			if (!TryGetSelectedNode(out node))
			{
				reason = "Select a node in Node Tree.";
				return false;
			}

			reason = string.Empty;
			return true;
		}

		public void SetMoveInViewportMode(bool enabled)
		{
			if (enabled == isMoveInViewportModeEnabled)
			{
				if (enabled)
				{
					Data.Node node;
					if (!TryGetSelectedNode(out node))
					{
						DisableMoveInViewportMode();
					}
				}
				return;
			}

			if (enabled)
			{
				Data.Node node;
				if (!TryGetSelectedNode(out node))
				{
					return;
				}

				isMoveInViewportModeEnabled = true;
				hoveredMoveAxis = MoveGizmoAxis.None;
				activeMoveAxis = MoveGizmoAxis.None;

				EnsureNodeUsesFixedPosition(node);
			}
			else
			{
				DisableMoveInViewportMode();
			}
		}

		public bool UpdateMoveGizmoInput(swig.Vec2 mousePos)
		{
			if (!isMoveInViewportModeEnabled)
			{
				hoveredMoveAxis = MoveGizmoAxis.None;
				return false;
			}

			Data.Node selectedNode;
			if (!TryGetSelectedNode(out selectedNode))
			{
				DisableMoveInViewportMode();
				return false;
			}

			if (isMoveGizmoDragging && dragTargetNode != selectedNode)
			{
				EndMoveGizmoDrag();
			}

			if (!hasViewportRect)
			{
				hoveredMoveAxis = MoveGizmoAxis.None;
				return false;
			}

			var isLeftDown = Manager.NativeManager.IsMouseDown(0);
			var isLeftClicked = Manager.NativeManager.IsMouseClicked(0, false);
			var isLeftReleased = Manager.NativeManager.IsMouseReleased(0);

			if (isMoveGizmoDragging)
			{
				if (!isLeftDown || isLeftReleased)
				{
					EndMoveGizmoDrag();
					return true;
				}

				MathRay mouseRay;
				if (!TryBuildMouseRay(mousePos, out mouseRay))
				{
					EndMoveGizmoDrag();
					return true;
				}

				var axisDirection = GetAxisDirection(activeMoveAxis);
				float currentAxisParameter;

				if (!TryCalculateAxisParameter(dragStartPosition, axisDirection, mouseRay, out currentAxisParameter))
				{
					currentAxisParameter = dragStartAxisParameter;
				}

				var position = dragStartPosition + axisDirection * (currentAxisParameter - dragStartAxisParameter);
				ApplyNodePosition(selectedNode, position);
				return true;
			}

			if (!ContainsInViewport(mousePos.X, mousePos.Y))
			{
				hoveredMoveAxis = MoveGizmoAxis.None;
				return false;
			}

			hoveredMoveAxis = PickMoveAxis(selectedNode, mousePos.X, mousePos.Y);

			if (isLeftClicked && hoveredMoveAxis != MoveGizmoAxis.None)
			{
				BeginMoveGizmoDrag(selectedNode, hoveredMoveAxis, mousePos);
				return true;
			}

			return false;
		}

		public bool TryGetMoveGizmoRenderInfo(out MoveGizmoRenderInfo renderInfo)
		{
			renderInfo = new MoveGizmoRenderInfo();

			if (!isMoveInViewportModeEnabled)
			{
				return false;
			}

			Data.Node node;
			if (!TryGetSelectedNode(out node))
			{
				return false;
			}

			var center = GetNodePosition(node);
			var axisLength = GetMoveGizmoAxisLength(center);
			if (axisLength <= 1e-5f)
			{
				return false;
			}

			renderInfo.CenterX = center.X;
			renderInfo.CenterY = center.Y;
			renderInfo.CenterZ = center.Z;
			renderInfo.AxisLength = axisLength;
			renderInfo.HoveredAxis = hoveredMoveAxis;
			renderInfo.ActiveAxis = activeMoveAxis;

			return true;
		}

		public override void OnDisposed()
		{
			DisableMoveInViewportMode();
			base.OnDisposed();
		}

		void DisableMoveInViewportMode()
		{
			EndMoveGizmoDrag();
			isMoveInViewportModeEnabled = false;
			hoveredMoveAxis = MoveGizmoAxis.None;
			activeMoveAxis = MoveGizmoAxis.None;
		}

		void ValidateMoveModeState()
		{
			if (!isMoveInViewportModeEnabled)
			{
				return;
			}

			Data.Node node;
			if (!TryGetSelectedNode(out node))
			{
				DisableMoveInViewportMode();
			}
		}

		bool TryGetSelectedNode(out Data.Node node)
		{
			node = Core.SelectedNode as Data.Node;
			return node != null;
		}

		void EnsureNodeUsesFixedPosition(Data.Node node)
		{
			if (node.LocationValues.Type.Value != Data.LocationValues.ParamaterType.Fixed)
			{
				node.LocationValues.Type.SetValue(Data.LocationValues.ParamaterType.Fixed);
			}
		}

		void BeginMoveGizmoDrag(Data.Node node, MoveGizmoAxis axis, swig.Vec2 mousePos)
		{
			if (axis == MoveGizmoAxis.None)
			{
				return;
			}

			EndMoveGizmoDrag();

			Command.CommandManager.StartCollection();
			isMoveCommandCollectionStarted = true;

			EnsureNodeUsesFixedPosition(node);

			var startPosition = GetNodePosition(node);

			MathRay mouseRay;
			if (!TryBuildMouseRay(mousePos, out mouseRay))
			{
				EndMoveGizmoDrag();
				return;
			}

			var axisDirection = GetAxisDirection(axis);

			float axisParameter;
			if (!TryCalculateAxisParameter(startPosition, axisDirection, mouseRay, out axisParameter))
			{
				axisParameter = 0.0f;
			}

			dragTargetNode = node;
			dragStartPosition = startPosition;
			dragStartAxisParameter = axisParameter;
			activeMoveAxis = axis;
			isMoveGizmoDragging = true;
		}

		void EndMoveGizmoDrag()
		{
			if (isMoveCommandCollectionStarted)
			{
				Command.CommandManager.EndCollection();
				isMoveCommandCollectionStarted = false;
			}

			dragTargetNode = null;
			isMoveGizmoDragging = false;
			activeMoveAxis = MoveGizmoAxis.None;
		}

		MathVector3 GetNodePosition(Data.Node node)
		{
			var location = node.LocationValues.Fixed.Location;
			return new MathVector3(location.X.Value, location.Y.Value, location.Z.Value);
		}

		void ApplyNodePosition(Data.Node node, MathVector3 position)
		{
			var location = node.LocationValues.Fixed.Location;
			location.X.SetValue(position.X);
			location.Y.SetValue(position.Y);
			location.Z.SetValue(position.Z);
		}

		MoveGizmoAxis PickMoveAxis(Data.Node node, float mouseX, float mouseY)
		{
			var center = GetNodePosition(node);
			var axisLength = GetMoveGizmoAxisLength(center);

			var mouse = new MathVector2(mouseX, mouseY);
			var threshold = 10.0f * Manager.DpiScale;
			var nearestDistance = float.MaxValue;
			var nearestAxis = MoveGizmoAxis.None;

			MoveGizmoAxis[] axes =
			{
				MoveGizmoAxis.X,
				MoveGizmoAxis.Y,
				MoveGizmoAxis.Z,
			};

			for (int i = 0; i < axes.Length; i++)
			{
				var axis = axes[i];
				var axisDir = GetAxisDirection(axis);

				MathVector2 p0;
				MathVector2 p1;

				if (!TryProjectToScreen(center, out p0))
				{
					continue;
				}

				if (!TryProjectToScreen(center + axisDir * axisLength, out p1))
				{
					continue;
				}

				var dist = DistancePointToSegment(mouse, p0, p1);
				if (dist <= threshold && dist < nearestDistance)
				{
					nearestDistance = dist;
					nearestAxis = axis;
				}
			}

			return nearestAxis;
		}

		bool TryBuildMouseRay(swig.Vec2 mousePos, out MathRay ray)
		{
			ray = new MathRay();

			if (!hasViewportRect || viewportRectSizeX <= 1 || viewportRectSizeY <= 1)
			{
				return false;
			}

			CameraBasis cameraBasis;
			if (!TryGetCameraBasis(out cameraBasis))
			{
				return false;
			}

			var localX = mousePos.X - viewportRectMinX;
			var localY = mousePos.Y - viewportRectMinY;

			var width = viewportRectSizeX;
			var height = viewportRectSizeY;

			var ndcX = localX / width * 2.0f - 1.0f;
			var ndcY = 1.0f - localY / height * 2.0f;

			var projectionType = Manager.Viewer.ViewPointController.GetProjectionType();

			if (projectionType == swig.ProjectionType.Perspective)
			{
				var tanHalfY = GetPerspectiveTanHalfFovY();
				var aspect = width / height;
				var direction = cameraBasis.Forward +
								cameraBasis.Right * (ndcX * tanHalfY * aspect) +
								cameraBasis.Up * (ndcY * tanHalfY);

				ray.Origin = cameraBasis.CameraOrigin;
				ray.Direction = direction.Normalized();
				return ray.Direction.Length() > 0.0f;
			}

			var scale = GetOrthographicScale() * Math.Max(0.0001f, Manager.Viewer.ViewPointController.RateOfMagnification);
			var offsetX = ndcX * width * 0.5f / scale;
			var offsetY = ndcY * height * 0.5f / scale;

			ray.Origin = cameraBasis.CameraOrigin + cameraBasis.Right * offsetX + cameraBasis.Up * offsetY;
			ray.Direction = cameraBasis.Forward;

			return true;
		}

		bool TryProjectToScreen(MathVector3 worldPosition, out MathVector2 screenPosition)
		{
			screenPosition = new MathVector2(0.0f, 0.0f);

			if (!hasViewportRect || viewportRectSizeX <= 1 || viewportRectSizeY <= 1)
			{
				return false;
			}

			CameraBasis cameraBasis;
			if (!TryGetCameraBasis(out cameraBasis))
			{
				return false;
			}

			var v = worldPosition - cameraBasis.CameraOrigin;

			var cameraX = MathVector3.Dot(v, cameraBasis.Right);
			var cameraY = MathVector3.Dot(v, cameraBasis.Up);
			var cameraZ = MathVector3.Dot(v, cameraBasis.Forward);

			var width = viewportRectSizeX;
			var height = viewportRectSizeY;

			float ndcX;
			float ndcY;

			var projectionType = Manager.Viewer.ViewPointController.GetProjectionType();
			if (projectionType == swig.ProjectionType.Perspective)
			{
				if (cameraZ <= 1e-5f)
				{
					return false;
				}

				var tanHalfY = GetPerspectiveTanHalfFovY();
				var aspect = width / height;

				ndcX = cameraX / (cameraZ * tanHalfY * aspect);
				ndcY = cameraY / (cameraZ * tanHalfY);
			}
			else
			{
				var scale = GetOrthographicScale() * Math.Max(0.0001f, Manager.Viewer.ViewPointController.RateOfMagnification);
				var halfWidth = width * 0.5f / scale;
				var halfHeight = height * 0.5f / scale;

				if (halfWidth <= 1e-5f || halfHeight <= 1e-5f)
				{
					return false;
				}

				ndcX = cameraX / halfWidth;
				ndcY = cameraY / halfHeight;
			}

			screenPosition.X = viewportRectMinX + (ndcX * 0.5f + 0.5f) * width;
			screenPosition.Y = viewportRectMinY + (0.5f - ndcY * 0.5f) * height;
			return true;
		}

		bool TryGetCameraBasis(out CameraBasis basis)
		{
			basis = new CameraBasis();

			if (Manager.Viewer == null || Manager.Viewer.ViewPointController == null)
			{
				return false;
			}

			var cameraRay = Manager.Viewer.ViewPointController.GetCameraRay();
			var forward = new MathVector3(cameraRay.Direction.X, cameraRay.Direction.Y, cameraRay.Direction.Z).Normalized();

			if (forward.Length() <= 1e-5f)
			{
				return false;
			}

			var upRef = new MathVector3(0.0f, 1.0f, 0.0f);
			if (Math.Abs(MathVector3.Dot(forward, upRef)) >= 0.99f)
			{
				upRef = new MathVector3(0.0f, 0.0f, 1.0f);
			}

			var isRightHand = Manager.Viewer.ViewPointController.GetCoordinateSystem() == swig.CoordinateSystemType.RH;

			var right = isRightHand ? MathVector3.Cross(forward, upRef) : MathVector3.Cross(upRef, forward);
			right = right.Normalized();
			if (right.Length() <= 1e-5f)
			{
				return false;
			}

			var up = isRightHand ? MathVector3.Cross(right, forward) : MathVector3.Cross(forward, right);
			up = up.Normalized();

			basis.CameraOrigin = new MathVector3(cameraRay.Origin.X, cameraRay.Origin.Y, cameraRay.Origin.Z);
			basis.Forward = forward;
			basis.Right = right;
			basis.Up = up;
			return true;
		}

		float GetMoveGizmoAxisLength(MathVector3 center)
		{
			var projectionType = Manager.Viewer.ViewPointController.GetProjectionType();
			if (projectionType == swig.ProjectionType.Perspective)
			{
				CameraBasis cameraBasis;
				if (!TryGetCameraBasis(out cameraBasis))
				{
					return 1.0f;
				}

				var distance = (center - cameraBasis.CameraOrigin).Length();
				return Math.Max(MinimumAxisLength, distance * 0.2f);
			}

			var pixels = 96.0f * Manager.DpiScale;
			var worldPerPixel = 1.0f / (GetOrthographicScale() * Math.Max(0.0001f, Manager.Viewer.ViewPointController.RateOfMagnification));
			return Math.Max(MinimumAxisLength, worldPerPixel * pixels);
		}

		float GetOrthographicScale()
		{
			var distance = Manager.Viewer.ViewPointController.GetDistance();
			if (distance <= 1e-5f)
			{
				return OrthoScaleBase;
			}

			return OrthoScaleBase * DistanceBase / distance;
		}

		float GetPerspectiveTanHalfFovY()
		{
			var rate = Math.Max(0.0001f, Manager.Viewer.ViewPointController.RateOfMagnification);
			return (float)Math.Tan(PerspectiveFovRad * 0.5f) / rate;
		}

		bool TryCalculateAxisParameter(MathVector3 axisOrigin, MathVector3 axisDirection, MathRay ray, out float axisParameter)
		{
			axisParameter = 0.0f;

			var w0 = axisOrigin - ray.Origin;
			var a = MathVector3.Dot(axisDirection, axisDirection);
			var b = MathVector3.Dot(axisDirection, ray.Direction);
			var c = MathVector3.Dot(ray.Direction, ray.Direction);
			var d = MathVector3.Dot(axisDirection, w0);
			var e = MathVector3.Dot(ray.Direction, w0);

			var denominator = a * c - b * b;
			if (Math.Abs(denominator) <= 1e-5f)
			{
				return false;
			}

			axisParameter = (b * e - c * d) / denominator;
			return true;
		}

		bool ContainsInViewport(float x, float y)
		{
			if (!hasViewportRect)
			{
				return false;
			}

			if (x < viewportRectMinX || x > viewportRectMinX + viewportRectSizeX)
			{
				return false;
			}

			if (y < viewportRectMinY || y > viewportRectMinY + viewportRectSizeY)
			{
				return false;
			}

			return true;
		}

		static float DistancePointToSegment(MathVector2 p, MathVector2 p0, MathVector2 p1)
		{
			var segment = p1 - p0;
			var segmentLengthSq = segment.LengthSquared();
			if (segmentLengthSq <= 1e-5f)
			{
				return (p - p0).Length();
			}

			var t = MathVector2.Dot(p - p0, segment) / segmentLengthSq;
			t = Math.Max(0.0f, Math.Min(1.0f, t));

			var nearest = p0 + segment * t;
			return (p - nearest).Length();
		}

		static MathVector3 GetAxisDirection(MoveGizmoAxis axis)
		{
			switch (axis)
			{
				case MoveGizmoAxis.X:
					return new MathVector3(1.0f, 0.0f, 0.0f);
				case MoveGizmoAxis.Y:
					return new MathVector3(0.0f, 1.0f, 0.0f);
				case MoveGizmoAxis.Z:
					return new MathVector3(0.0f, 0.0f, 1.0f);
				default:
					return new MathVector3(0.0f, 0.0f, 0.0f);
			}
		}

		protected void DrawMainImage(swig.Vec2 contentSize, float frameHeight, float padding)
		{
			// Menu
			contentSize.X = System.Math.Max(1, contentSize.X);
			contentSize.Y = System.Math.Max(1, contentSize.Y - frameHeight - padding);

			Manager.Viewer.ViewPointController.SetScreenSize((int)contentSize.X, (int)contentSize.Y);
			Manager.Viewer.ViewPointController.Update();

			var ray = Manager.Viewer.ViewPointController.GetCameraRay();

			var renderParam = Manager.Viewer.EffectRenderer.GetParameter();
			renderParam.CameraMatrix = Manager.Viewer.ViewPointController.GetCameraMatrix();
			renderParam.ProjectionMatrix = Manager.Viewer.ViewPointController.GetProjectionMatrix();
			renderParam.CameraPosition = ray.Origin;
			renderParam.CameraFrontDirection = ray.Direction;
			Manager.Viewer.EffectRenderer.SetParameter(renderParam);

			Manager.Viewer.EffectRenderer.ResizeScreen(Manager.Viewer.ViewPointController.GetScreenSize());
			Manager.MainViewImage.Resize((int)contentSize.X, (int)contentSize.Y);

			Manager.Viewer.EffectRenderer.Render(Manager.MainViewImage);

			if (deviceType == swig.DeviceType.OpenGL)
			{
				Manager.NativeManager.ImageData(Manager.MainViewImage, (int)contentSize.X, (int)contentSize.Y, 0, 1, 1, 0);
			}
			else
			{
				Manager.NativeManager.ImageData(Manager.MainViewImage, (int)contentSize.X, (int)contentSize.Y);
			}

			viewportRectMinX = Manager.NativeManager.GetItemRectMinX();
			viewportRectMinY = Manager.NativeManager.GetItemRectMinY();
			viewportRectSizeX = Manager.NativeManager.GetItemRectSizeX();
			viewportRectSizeY = Manager.NativeManager.GetItemRectSizeY();
			hasViewportRect = viewportRectSizeX > 1.0f && viewportRectSizeY > 1.0f;
		}

		private static int getLodIndexFromLodBit(int lodBits)
		{
			if (lodBits == 0)
			{
				return 0;
			}
			return (int)((Math.Log10(lodBits & -lodBits)) / Math.Log10(2));
		}
		protected override void UpdateInternal()
		{
			ValidateMoveModeState();

			float textHeight = Manager.NativeManager.GetTextLineHeight();
			float frameHeight = Manager.NativeManager.GetFrameHeightWithSpacing();
			float dpiScale = Manager.DpiScale;
			float padding = 4.0f * dpiScale;

			IsHovered = false;

			var contentSize = Manager.NativeManager.GetContentRegionAvail();

			// Menu
			DrawMainImage(contentSize, frameHeight, padding);

			IsHovered = Manager.NativeManager.IsWindowHovered();

			Manager.NativeManager.Indent(padding);

			// Enum
			Manager.NativeManager.PushItemWidth(textHeight * 7.0f);
			renderMode.Update();
			Manager.NativeManager.PopItemWidth();

			Manager.NativeManager.SameLine();

			Manager.NativeManager.PushItemWidth(textHeight * 2.5f);
			viewMode.Update();
			Manager.NativeManager.PopItemWidth();

			
			string perfText =
				"Current LOD: " + getLodIndexFromLodBit(Manager.Viewer.EffectRenderer.GetCurrentLOD()) + "  " + 
				"D:" + Manager.Viewer.EffectRenderer.GetAndResetDrawCall().ToString("D3") + "  " +
				"V:" + Manager.Viewer.EffectRenderer.GetAndResetVertexCount().ToString("D5") + "  " +
				"P:" + Manager.Viewer.EffectRenderer.GetInstanceCount().ToString("D5") + " ";

			Manager.NativeManager.SameLine(contentSize.X - Manager.NativeManager.CalcTextSize(perfText).X);

			// Display performance information
			Manager.NativeManager.Text(perfText);
			if (Manager.NativeManager.IsItemHovered())
			{
				Manager.NativeManager.SetTooltip(
					"Current LOD: Level of Detail is currently utilized\n" +
					"D: Draw calls of current rendering.\n" +
					"V: Vertex count of current rendering.\n" +
					"P: Particle count of current rendering.");
			}
		}
	}
}
