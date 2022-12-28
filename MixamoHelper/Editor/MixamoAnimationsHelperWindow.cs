using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace nGAGEOnline.Tools.MixamoAnimationHelper.MixamoHelper.Editor
{
	public class MixamoAnimationsHelperWindow : EditorWindow
	{
		#region CONSTS

		private const string DEFAULT_AVATAR = "Y Bot@T-Pose";
		private const string MIXAMO_FIXER_AVATAR = "MixamoFixer_Avatar";
		private const string MIXAMO_FIXER_AVATAR_SETUP = "MixamoFixer_AvatarSetup";
		private const string MIXAMO_FIXER_LOOP_TIME = "MixamoFixer_LoopTime";
		private const string MIXAMO_FIXER_BAKE_ROTATION = "MixamoFixer_Rotation";
		private const string MIXAMO_FIXER_BAKE_POSITION_Y = "MixamoFixer_PositionY";
		private const string MIXAMO_FIXER_BAKE_POSITION_XZ = "MixamoFixer_PositionXZ";
		private const string MIXAMO_FIXER_FIX_ANIMATION_NAME = "MixamoFixer_FixAnimationName";

		#endregion

		[SerializeField] private ModelImporterAvatarSetup _avatarSetup = ModelImporterAvatarSetup.CopyFromOther;
		[SerializeField] private Avatar _avatar;
		[SerializeField] private bool _loopTime = true;
		[SerializeField] private bool _bakeRotation = true;
		[SerializeField] private bool _bakePositionY = true;
		[SerializeField] private bool _bakePositionXZ = false;
		[SerializeField] private bool _fixAnimationName = true;
	
		private SerializedObject _so;
		private SerializedProperty _propAvatarSetup;
		private SerializedProperty _propAvatar;
		private SerializedProperty _propLoopTime;
		private SerializedProperty _propBakeRotation;
		private SerializedProperty _propBakePositionY;
		private SerializedProperty _propBakePositionXZ;
		private SerializedProperty _propFixAnimationName;

		private void OnEnable()
		{
			_so = new SerializedObject(this);
			_propAvatarSetup = _so.FindProperty(nameof(_avatarSetup));
			_propAvatar = _so.FindProperty(nameof(_avatar));
			_propLoopTime = _so.FindProperty(nameof(_loopTime));
			_propBakeRotation = _so.FindProperty(nameof(_bakeRotation));
			_propBakePositionY = _so.FindProperty(nameof(_bakePositionY));
			_propBakePositionXZ = _so.FindProperty(nameof(_bakePositionXZ));
			_propFixAnimationName = _so.FindProperty(nameof(_fixAnimationName));

			LoadPreferences();
		}

		private void OnGUI()
		{
			_so.Update();

			using (new GUILayout.VerticalScope(new GUIStyle(EditorStyles.helpBox)))
			{
				EditorGUILayout.LabelField("Mixamo Animation Helper Preferences", new GUIStyle(EditorStyles.boldLabel));
			
				using (new GUILayout.VerticalScope(new GUIStyle(EditorStyles.helpBox)))
				{
					EditorGUILayout.PropertyField(_propAvatarSetup);
					if (_avatarSetup == ModelImporterAvatarSetup.CopyFromOther)
						EditorGUILayout.PropertyField(_propAvatar);
				}
			
				using (new GUILayout.VerticalScope(new GUIStyle(EditorStyles.helpBox)))
				{
					EditorGUILayout.PropertyField(_propLoopTime);
					EditorGUILayout.PropertyField(_propBakeRotation);
					EditorGUILayout.PropertyField(_propBakePositionY);
					EditorGUILayout.PropertyField(_propBakePositionXZ);
					EditorGUILayout.PropertyField(_propFixAnimationName);
				}
			
				using (new GUILayout.HorizontalScope())
				{
					if (GUILayout.Button("Clear Preferences"))
						ClearPreferences();
					
					GUILayout.FlexibleSpace();

					if (!_avatar)
						if (GUILayout.Button("Load Defaults"))
							DefaultPreferences();
				}
				
				if (HasUnsavedPreferences())
					using (new GUILayout.VerticalScope(new GUIStyle(EditorStyles.helpBox)))
						EditorGUILayout.HelpBox("One or more preferences were not saved.", 
							MessageType.Warning, true);
					
				
				using (new GUILayout.VerticalScope(new GUIStyle(EditorStyles.helpBox)))
					EditorGUILayout.HelpBox("If the AVATAR field is empty \"Load Default Avatar\" to populate it with a working T-pose avatar.\n\n" +
					                        "This should be enough to get started. You may also choose one of your own avatars, but this may NOT work.\n\n" +
					                        "You can now right-click one (or more) Mixamo animations and under \"QuickAccess\" you'll be able to \"Apply Mixamo Animation Fix\" to fixing them.",
						MessageType.Info, true);
			}

			if (_so.hasModifiedProperties)
			{
				_so.ApplyModifiedProperties();
				SavePreferences();
			}

			if (Event.current.type != EventType.MouseDown || Event.current.button != 0)
				return;
				
			GUI.FocusControl(null);
			Repaint();
		}
		
		[MenuItem("nGAGEOnline/Mixamo Animation Helper", priority = 40)] // % = Ctrl | # = Shift | & = Alt
		public static void OpenWindow()
		{
			var window = GetWindow<MixamoAnimationsHelperWindow>();
			window.titleContent = new GUIContent("Mixamo Animation Helper");
			window.minSize = new Vector2(400, 350);
			window.maxSize = new Vector2(400, 350);
			window.LoadPreferences();
		}
		
		[MenuItem("Assets/Animation Helper/Apply Mixamo Animation Fix", false, priority = 104)]
		public static void FixAnimation()
		{
			foreach (var path in GetPaths())
			{
				var target = AssetDatabase.LoadAssetAtPath<GameObject>(path);
				if (AssetImporter.GetAtPath(path) is not ModelImporter importer)
					continue;
			
				importer.importAnimation = true;

				if (importer.defaultClipAnimations.Length > 1)
				{
					Debug.Log($"[SKIPPING] More than one animation detected: {path}");
					// TODO: Custom naming of multiple animations per file?
					// following for-loop can handle simple multiple animations but we're skipping for now
					continue;
				}
				var modelImporterClipAnimations = new ModelImporterClipAnimation[importer.defaultClipAnimations.Length];
				for (var i = 0; i < importer.defaultClipAnimations.Length; i++)
				{
					var animation = importer.defaultClipAnimations[i];
					if (EditorPrefs.GetBool(MIXAMO_FIXER_FIX_ANIMATION_NAME))
						animation.name = i == 0 ? target.name : $"{target.name}_{i}";
					animation.loopTime = EditorPrefs.GetBool(MIXAMO_FIXER_LOOP_TIME);
					animation.lockRootRotation = EditorPrefs.GetBool(MIXAMO_FIXER_BAKE_ROTATION);
					animation.lockRootHeightY = EditorPrefs.GetBool(MIXAMO_FIXER_BAKE_POSITION_Y);
					animation.lockRootPositionXZ = EditorPrefs.GetBool(MIXAMO_FIXER_BAKE_POSITION_XZ);
					animation.keepOriginalOrientation = true;
					animation.keepOriginalPositionY = true;
					animation.keepOriginalPositionXZ = true;
					modelImporterClipAnimations[i] = animation;
				}
				importer.animationType = ModelImporterAnimationType.Human;
				importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
				var avatar = GetAvatarFromPrefs();
				if (avatar)
				{
					importer.avatarSetup = ModelImporterAvatarSetup.CopyFromOther;
					importer.sourceAvatar = avatar; 
				}
				importer.clipAnimations = modelImporterClipAnimations;
				importer.SaveAndReimport();
			}
			AssetDatabase.Refresh();
		}
		
		[MenuItem("Assets/Animation Helper/Apply Mixamo Animation Fix", true, priority = 104)]
		public static bool ValidateFixAnimation() => IsModel(Selection.activeObject);

		[MenuItem("Assets/Animation Helper/Fix Mixamo Names", false, priority = 105)]
		public static void Rename()
		{
			foreach (var path in GetPaths())
			{
				var target = AssetDatabase.LoadAssetAtPath<GameObject>(path);
				if (AssetImporter.GetAtPath(path) is not ModelImporter importer)
					continue;
			
				importer.importAnimation = true;

				if (importer.defaultClipAnimations.Length > 1)
				{
					Debug.Log($"[SKIPPING] More than one animation detected: {path}");
					// TODO: Custom naming of multiple animations per file?
					// following for-loop can handle simple multiple animations but we're skipping for now
					continue;
				}
				var modelImporterClipAnimations = new ModelImporterClipAnimation[importer.defaultClipAnimations.Length];
				for (var i = 0; i < importer.defaultClipAnimations.Length; i++)
				{
					var animation = importer.defaultClipAnimations[i];
					animation.name = i == 0 ? target.name : $"{target.name}_{i}";
					modelImporterClipAnimations[i] = animation;
				}

				importer.clipAnimations = modelImporterClipAnimations;
				importer.SaveAndReimport();
			}
			AssetDatabase.Refresh();
		}
		
		[MenuItem("Assets/Animation Helper/Fix Mixamo Names", true, priority = 105)]
		public static bool ValidateRename() => IsModel(Selection.activeObject);

		[MenuItem("Assets/Animation Helper/Find Mixamo Doubles", false, priority = 106)]
		public static void FindDoubles()
		{
			var allObjects = new List<GameObject>();
			var sameFilenames = new Dictionary<string, List<GameObject>>();
			foreach (var path in GetPaths())
			{
				var asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
				if (allObjects.Any(x => x.name == asset.name))
				{
					if (!sameFilenames.ContainsKey(asset.name))
						sameFilenames.Add(asset.name, new List<GameObject>());
					sameFilenames[asset.name].Add(asset);
				}
				allObjects.Add(asset);
			}

			var duplicateClips = new Dictionary<string, List<GameObjectInfo>>();
			var uniqueClips = new Dictionary<string, List<GameObjectInfo>>();
			foreach (var objectInfo in sameFilenames
				         .SelectMany(x => x.Value)
				         .Select(y => new GameObjectInfo(y)))
			{
				if (!uniqueClips.ContainsKey(objectInfo.Name))
					uniqueClips.Add(objectInfo.Name, new List<GameObjectInfo>());

				if (!uniqueClips[objectInfo.Name].Any(x => x.StartTime == objectInfo.StartTime && x.StopTime == objectInfo.StopTime))
					uniqueClips[objectInfo.Name].Add(objectInfo);
				else
				{
					if (!duplicateClips.ContainsKey(objectInfo.Name))
						duplicateClips.Add(objectInfo.Name, new List<GameObjectInfo>());
					duplicateClips[objectInfo.Name].Add(objectInfo);
				}
			}
		}
		
		[MenuItem("Assets/Animation Helper/Find Mixamo Doubles", true, priority = 106)]
		public static bool ValidateFindDoubles() => IsModel(Selection.activeObject);

		private static bool IsModel(Object selection)
		{
			if (selection == null)
				return false;

			var path = AssetDatabase.GetAssetPath(selection);
			return AssetImporter.GetAtPath(path) is ModelImporter;
		}

		private static IEnumerable<string> GetPaths()
		{
			var paths = Selection.assetGUIDs.Select(AssetDatabase.GUIDToAssetPath);
			foreach (var path in paths)
			{
				if (!AssetDatabase.IsValidFolder(path))
					yield return path;
				else
				{
					var childPaths  = AssetDatabase.FindAssets("t:gameObject", new[] { path });
					foreach (var child in childPaths.Select(AssetDatabase.GUIDToAssetPath))
						if (new FileInfo(child).Extension.ToLower() == ".fbx")
							yield return child;
				}
			}
		}
		
		private void LoadPreferences()
		{
			if (!EditorPrefs.HasKey(MIXAMO_FIXER_AVATAR))
				EditorPrefs.SetString(MIXAMO_FIXER_AVATAR, AssetDatabase.GetAssetPath(ResourceAvatar()));
			_avatar = GetAvatarFromPrefs();
			
			if (!EditorPrefs.HasKey(MIXAMO_FIXER_AVATAR_SETUP))
				EditorPrefs.SetInt(MIXAMO_FIXER_AVATAR_SETUP, (int)ModelImporterAvatarSetup.CopyFromOther);
			_avatarSetup = (ModelImporterAvatarSetup)EditorPrefs.GetInt(MIXAMO_FIXER_AVATAR_SETUP);

			if (!EditorPrefs.HasKey(MIXAMO_FIXER_LOOP_TIME))
				EditorPrefs.SetBool(MIXAMO_FIXER_LOOP_TIME, true);
			_loopTime = EditorPrefs.GetBool(MIXAMO_FIXER_LOOP_TIME);
			
			if (!EditorPrefs.HasKey(MIXAMO_FIXER_BAKE_ROTATION))
				EditorPrefs.SetBool(MIXAMO_FIXER_BAKE_ROTATION, true);
			_bakeRotation = EditorPrefs.GetBool(MIXAMO_FIXER_BAKE_ROTATION);
			
			if (!EditorPrefs.HasKey(MIXAMO_FIXER_BAKE_POSITION_Y))
				EditorPrefs.SetBool(MIXAMO_FIXER_BAKE_POSITION_Y, true);
			_bakePositionY = EditorPrefs.GetBool(MIXAMO_FIXER_BAKE_POSITION_Y);
			
			if (!EditorPrefs.HasKey(MIXAMO_FIXER_BAKE_POSITION_XZ))
				EditorPrefs.SetBool(MIXAMO_FIXER_BAKE_POSITION_XZ, false);
			_bakePositionXZ = EditorPrefs.GetBool(MIXAMO_FIXER_BAKE_POSITION_XZ);
			
			if (!EditorPrefs.HasKey(MIXAMO_FIXER_FIX_ANIMATION_NAME))
				EditorPrefs.SetBool(MIXAMO_FIXER_FIX_ANIMATION_NAME, true);
			_fixAnimationName = EditorPrefs.GetBool(MIXAMO_FIXER_FIX_ANIMATION_NAME);
		}
		private void SavePreferences()
		{
			EditorPrefs.SetInt(MIXAMO_FIXER_AVATAR_SETUP, (int)_avatarSetup);
			EditorPrefs.SetString(MIXAMO_FIXER_AVATAR, _avatar 
				? AssetDatabase.GetAssetPath(_avatar) 
				: "");
			EditorPrefs.SetBool(MIXAMO_FIXER_LOOP_TIME, _loopTime);
			EditorPrefs.SetBool(MIXAMO_FIXER_BAKE_ROTATION, _bakeRotation);
			EditorPrefs.SetBool(MIXAMO_FIXER_BAKE_POSITION_Y, _bakePositionY);
			EditorPrefs.SetBool(MIXAMO_FIXER_BAKE_POSITION_XZ, _bakePositionXZ);
			EditorPrefs.SetBool(MIXAMO_FIXER_FIX_ANIMATION_NAME, _fixAnimationName);
		}

		private void DefaultPreferences()
		{
			_propAvatar.objectReferenceValue = ResourceAvatar();
			_propAvatarSetup.enumValueIndex = (int)ModelImporterAvatarSetup.CopyFromOther;
			_propLoopTime.boolValue = true;
			_propBakeRotation.boolValue = true;
			_propBakePositionY.boolValue = true;
			_propBakePositionXZ.boolValue = false;
			_propFixAnimationName.boolValue = true;
		}
		private void ClearPreferences()
		{
			_avatar = null;
			_avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
			_loopTime = false;
			_bakeRotation = false;
			_bakePositionY = false;
			_bakePositionXZ = false;
			_fixAnimationName = false;
			
			EditorPrefs.DeleteKey(MIXAMO_FIXER_AVATAR);
			EditorPrefs.DeleteKey(MIXAMO_FIXER_AVATAR_SETUP);
			EditorPrefs.DeleteKey(MIXAMO_FIXER_LOOP_TIME);
			EditorPrefs.DeleteKey(MIXAMO_FIXER_BAKE_ROTATION);
			EditorPrefs.DeleteKey(MIXAMO_FIXER_BAKE_POSITION_Y);
			EditorPrefs.DeleteKey(MIXAMO_FIXER_BAKE_POSITION_XZ);
			EditorPrefs.DeleteKey(MIXAMO_FIXER_FIX_ANIMATION_NAME);
		}

		private static Avatar ResourceAvatar() => Resources.Load<Avatar>(DEFAULT_AVATAR);
		private static Avatar GetAvatarFromPrefs()
		{
			var path = EditorPrefs.GetString(MIXAMO_FIXER_AVATAR);
			return AssetDatabase.LoadAssetAtPath<Avatar>(path);
		}
		
		private static bool HasUnsavedPreferences() => !EditorPrefs.HasKey(MIXAMO_FIXER_AVATAR) ||
		                                               !EditorPrefs.HasKey(MIXAMO_FIXER_AVATAR_SETUP) ||
		                                               !EditorPrefs.HasKey(MIXAMO_FIXER_LOOP_TIME) ||
		                                               !EditorPrefs.HasKey(MIXAMO_FIXER_BAKE_ROTATION) ||
		                                               !EditorPrefs.HasKey(MIXAMO_FIXER_BAKE_POSITION_Y) ||
		                                               !EditorPrefs.HasKey(MIXAMO_FIXER_BAKE_POSITION_XZ) ||
		                                               !EditorPrefs.HasKey(MIXAMO_FIXER_FIX_ANIMATION_NAME);

		private class GameObjectInfo
		{
			public string Name => GameObject.name;
			private ModelImporter Importer { get; }
			private GameObject GameObject { get; }
			private TakeInfo[] ImportedTakeInfos => Importer.importedTakeInfos;
			public string StartTime => ImportedTakeInfos[0].startTime.ToString(CultureInfo.InvariantCulture);
			public string StopTime => ImportedTakeInfos[0].stopTime.ToString(CultureInfo.InvariantCulture);

			public GameObjectInfo(Object gameObject)
				: this(AssetDatabase.GetAssetPath(gameObject))
			{ }

			private GameObjectInfo(string path)
			{
				GameObject = AssetDatabase.LoadAssetAtPath<GameObject>(path);
				Importer = AssetImporter.GetAtPath(path) as ModelImporter;
			}
		}
	}
}