using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using System;
using UnityEditor.AnimatedValues;

public abstract class EntityConfigAssetEditorBase<T1, T2> : Editor
	where T1 : ConfigScriptableObject
	where T2 : EntityConfigAsset<T1>
{
	private const int FOLDOUT_WIDTH = 13;
	private readonly Color HEADER_EVEN_ID_COLOR = new Color(0, 0, 0, 0.1f);
	private readonly Color HEADER_ODD_ID_COLOR = new Color(0, 0, 0, 0.05f);
	private readonly Color HEADER_HOVER_COLOR = new Color(1, 1, 1, 0.05f);
	private readonly Color HEADER_SELECTED_COLOR = new Color(0, 0.4f, 1, 0.25f);
	private readonly Color DARK_ICON_COLOR = new Color(0.9f, 0.9f, 0.9f, 1);

	protected T2 entityConfigAsset;
	protected GUIStyle iconButtonStyle = new GUIStyle();
	protected GUIStyle componentListStyle = new GUIStyle();

	public EntityConfigEditorInstance editorInstance;

	private static int _componentEditMode = 0;
	private string[] _componentEditModeStrings = new string[] {"Edit mode", "Reorder mode"};
	private SerializedProperty _componentsProperty;
	private float _headerHeight = 18;
	private Rect[] _headerRects;
	private int _hoveringComponentId = -1;
	private bool[] _selectedComponents;

	protected virtual void OnEnable()
	{
		entityConfigAsset = (T2)target;

		iconButtonStyle.normal.background = null;
		iconButtonStyle.active.background = null;
		iconButtonStyle.hover.background = null;

		_componentsProperty = serializedObject.FindProperty("components");
		RegenerateEditors();
	}

	private void OnGUI()
	{
		Event e = Event.current;
		if (_hoveringComponentId == -1 && e.type == EventType.MouseDown)
		{
			for (int i = 0; i < _selectedComponents.Length; i++)
				_selectedComponents[i] = false;
		}
	}

	protected void DrawComponentList()
	{
		bool wasHoveringOnComponent = false;
		var previousComponentEditMode = _componentEditMode;
		_componentEditMode = GUILayout.Toolbar (_componentEditMode, _componentEditModeStrings);

		if (previousComponentEditMode != _componentEditMode)
		{
			ApplyChanges();
			RegenerateEditors();
		}

		switch (_componentEditMode)
		{
			case 0:
				float oneLineHeight = EditorGUIUtility.singleLineHeight;
				int editorCount = editorInstance.editors.Length;

				entityConfigAsset.foldedOut = EditorExt.FoldoutHeader("Components", entityConfigAsset.foldedOut);

				if (EditorGUILayout.BeginFadeGroup(entityConfigAsset.foldedOut.faded))
				{
					EditorExt.BeginBoxGroup();
						for (int i = 0; i < editorCount; i++)
						{
							// EditorExt.BeginBoxGroup();
							EditorGUI.indentLevel++;
							Editor editor = editorInstance.editors[i];

							using (var check = new EditorGUI.ChangeCheckScope())
							{
								// Header
								Rect headerRect = GUILayoutUtility.GetRect(GUIContent.none, GUIStyle.none, GUILayout.ExpandWidth(true), GUILayout.Height(_headerHeight));
								Rect headerSelectRect = headerRect;
								headerSelectRect.x -= 3;
								headerSelectRect.y -= 1;
								headerSelectRect.width += 6;
								headerSelectRect.height += 2;

								if (Event.current.type == EventType.Repaint)
        							_headerRects[i] = headerRect;

								if (_selectedComponents[i])
									EditorGUI.DrawRect(headerSelectRect, HEADER_SELECTED_COLOR);
								else if (_hoveringComponentId == i)
									EditorGUI.DrawRect(headerSelectRect, HEADER_HOVER_COLOR);
								else if (i % 2 == 0)
									EditorGUI.DrawRect(headerSelectRect, HEADER_EVEN_ID_COLOR);
								else
									EditorGUI.DrawRect(headerSelectRect, HEADER_ODD_ID_COLOR);

								GUILayout.BeginArea(_headerRects[i]);
								GUILayout.BeginHorizontal();

									int oldIndentLevel = EditorGUI.indentLevel;
									bool canBeFoldedOut = false;
									bool nullComponent = entityConfigAsset.components[i] == null;
									EditorGUI.indentLevel = 0;

									if (!nullComponent)
									{
										EditorGUI.BeginDisabledGroup(true);
										var iterator = editor.serializedObject.GetIterator();
										if (entityConfigAsset.components[i].alwaysEnableFoldout || iterator.CountRemaining() > 1)
										{
											canBeFoldedOut = true;
											EditorGUILayout.Toggle(entityConfigAsset.components[i].foldedOut.target, EditorStyles.foldout, GUILayout.Width(FOLDOUT_WIDTH), GUILayout.ExpandHeight(true));
										}
										EditorGUI.EndDisabledGroup();
									}

									float leftPadding = FOLDOUT_WIDTH + 3;

									if (!canBeFoldedOut)
										GUILayout.Space(leftPadding);

									if (nullComponent)
									{
										EditorGUILayout.LabelField(EditorGUIUtility.IconContent("Warning@2x"), GUILayout.Width(25), GUILayout.ExpandHeight(true));
									}
									else
									{
										if (EditorGUIUtility.isProSkin)
											EditorGUILayout.LabelField(EditorGUIUtility.IconContent("d_ScriptableObject On Icon"), GUILayout.Width(18), GUILayout.ExpandHeight(true));
										else
											EditorExt.LabelFieldColor(new Color(0, 0, 0, 0.5f), EditorGUIUtility.IconContent("ScriptableObject On Icon"), GUILayout.Width(18), GUILayout.ExpandHeight(true));
											
										GUILayout.Label(entityConfigAsset.components[i].name, EditorStyles.boldLabel, GUILayout.MaxWidth(250), GUILayout.ExpandHeight(true));
									}

									var buttonRect = GUILayoutUtility.GetLastRect();
									var xMax = buttonRect.xMax;
									buttonRect.x = _headerRects[i].x - 25;
									buttonRect.xMax = xMax;
									buttonRect.y -= 1;
									buttonRect.height += 2;

									Event current = Event.current;
									var eventType = current.type;

									if (buttonRect.Contains(current.mousePosition))
									{
										wasHoveringOnComponent = true;
										_hoveringComponentId = i;

										if (eventType == EventType.MouseDown && current.button == 0)
										{
											if (current.control)
											{
												_selectedComponents[i] = !_selectedComponents[i];
											}
											else
											{
												if (canBeFoldedOut && !nullComponent)
												{
													if (current.clickCount > 1 || current.mousePosition.x < buttonRect.x + leftPadding)
														ToggleComponentFoldout(i);
													else
														HighlightSpecificComponent(i);
												}
												else
													HighlightSpecificComponent(i);
											}
										}
										else if (eventType == EventType.ContextClick)
										{
											GenericMenu menu = new GenericMenu();
											int selectedComponentCount = 0;
											foreach (var sel in _selectedComponents)
												if (sel) selectedComponentCount++;

											if (i == 0 || selectedComponentCount > 1)
												menu.AddDisabledItem(new GUIContent("Move Up"), false);
											else
												menu.AddItem(new GUIContent("Move Up"), false, OnMenuMoveComponentUp, i);

											if (i == editorCount - 1 || selectedComponentCount > 1)
												menu.AddDisabledItem(new GUIContent("Move Down"), false);
											else
												menu.AddItem(new GUIContent("Move Down"), false, OnMenuMoveComponentDown, i);

											// menu.AddSeparator("");
											// menu.AddItem(new GUIContent("Select Asset"), false, OnMenuSelectComponentAsset, i);
											menu.AddSeparator("");
											if (selectedComponentCount > 1)
												menu.AddItem(new GUIContent("Remove Components"), false, OnMenuRemoveSelectedComponents);
											else
												menu.AddItem(new GUIContent("Remove Component"), false, OnMenuRemoveSelectedComponents);
											menu.ShowAsContext();

											current.Use(); 
										}
									}

									entityConfigAsset.components[i] = (T1)EditorGUILayout.ObjectField(entityConfigAsset.components[i], typeof(T1), false, GUILayout.ExpandWidth(true), GUILayout.Height(_headerHeight));

									// if (GUILayout.Button("-", GUILayout.Width(oneLineHeight), GUILayout.ExpandHeight(true)))
									GUILayout.Label("", GUILayout.MaxWidth(10), GUILayout.Height(_headerHeight));
									Rect closeButtonRect = GUILayoutUtility.GetLastRect();
									var closeImage = EditorGUIUtility.isProSkin
										? EditorGUIUtility.IconContent("d_winbtn_win_close").image
										: EditorGUIUtility.IconContent("winbtn_win_close").image;
									closeButtonRect.x -= 4;
									closeButtonRect.y = _headerHeight * 0.5f - closeImage.height * 0.5f;
									closeButtonRect.width = closeImage.width;
									closeButtonRect.height = closeImage.height;

									GUI.DrawTexture(closeButtonRect, closeImage);
									// GUI.Label(closeButtonRect, EditorGUIUtility.IconContent("d_winbtn_win_close_a@2x"));
									// if (GUILayout.Button("EditorGUIUtility.IconContent("d_winbtn_win_close_a@2x")", GUIStyle.none, GUILayout.Width(oneLineHeight), GUILayout.ExpandHeight(true)))
									if (GUI.Button(closeButtonRect, "", GUIStyle.none))
									{
										entityConfigAsset.components.RemoveAt(i);
										RegenerateEditors();
										return;
									}
									EditorGUI.indentLevel = oldIndentLevel;
									
									GUI.color = Color.white;
								GUILayout.EndHorizontal();
								GUILayout.EndArea();
								
								GUILayout.Space(2);

								if (check.changed)
								{
									RegenerateEditors();
									return;
								}

								// Component Editor
								if (entityConfigAsset.components[i] != null)
								{
									// if (entityConfigAsset.components[i] != null && entityConfigAsset.components[i].foldedOut.)
									if (EditorGUILayout.BeginFadeGroup(entityConfigAsset.components[i].foldedOut.faded))
									{
										// EditorExt.BeginBoxGroup();
										EditorGUI.indentLevel++;
											editor.OnInspectorGUI();
										EditorGUI.indentLevel--;
										// EditorExt.EndBoxGroup();
									}
									EditorGUILayout.EndFadeGroup();
								}

								if (check.changed)
									ApplyChanges();
							}

							EditorGUI.indentLevel--;
							// EditorExt.EndBoxGroup();
						}

						GUILayout.Space(3);

						// Add Component area
						GUILayout.BeginHorizontal();
							GUILayout.FlexibleSpace();
								if (GUILayout.Button("Add Component", GUILayout.Width(200), GUILayout.Height(oneLineHeight + 6)))
								{
									entityConfigAsset.components.Add(null);
									ApplyChanges();
									RegenerateEditors();
								}
						GUILayout.FlexibleSpace();
						GUILayout.EndHorizontal();

						GUILayout.Space(3);

					EditorExt.EndBoxGroup();
				}
				EditorGUILayout.EndFadeGroup();

				if (_hoveringComponentId == -1 && Event.current.type == EventType.MouseDown)
				{
					for (int i = 0; i < _selectedComponents.Length; i++)
						_selectedComponents[i] = false;
				}

				// Drag items from the assets window
				if (Event.current.type == EventType.DragUpdated)
				{
					DragAndDrop.visualMode = DragAndDropVisualMode.Link;
					Event.current.Use();
				}
				else if (Event.current.type == EventType.DragPerform)
				{
					bool componentsUpdated = false;
					DragAndDrop.AcceptDrag();

					if (DragAndDrop.paths.Length == DragAndDrop.objectReferences.Length)
					{
						for (int i = 0; i < DragAndDrop.objectReferences.Length; i++)
						{
							object obj = DragAndDrop.objectReferences[i];
							string path = DragAndDrop.paths[i];

							if (obj is T1 configAsset)
							{
								entityConfigAsset.components.Add(configAsset);
								ApplyChanges();
								componentsUpdated = true;
							}
						}
					}

					if (componentsUpdated)
						RegenerateEditors();
				}
			break;
			case 1:
				EditorGUILayout.PropertyField(_componentsProperty);
			break;
		}

		GUI.color = Color.white;

		if (!wasHoveringOnComponent && Event.current.type == EventType.Repaint)
			_hoveringComponentId = -1;

		Repaint();
	}

	private void HighlightSpecificComponent(int index)
	{
		for (int i = 0; i < _selectedComponents.Length; i++)
			_selectedComponents[i] = index == i;
	}

	private void ToggleComponentFoldout(int i) => entityConfigAsset.components[i].foldedOut.target = !entityConfigAsset.components[i].foldedOut.target;

	private void OnMenuMoveComponentDown(object userData)
	{
		var index = (int)userData;
		if (index == entityConfigAsset.components.Count - 1)
			return;

		MoveListItem(ref entityConfigAsset.components, index, index + 1);
		for (int i = 0; i < _selectedComponents.Length; i++)
			_selectedComponents[i] = i == index + 1; 

		RegenerateEditors();
	}

	private void OnMenuMoveComponentUp(object userData)
	{
		var index = (int)userData;
		if (index == 0)
			return;

		MoveListItem(ref entityConfigAsset.components, index, index - 1);
		for (int i = 0; i < _selectedComponents.Length; i++)
			_selectedComponents[i] = i == index - 1;

		RegenerateEditors();
	}

	private void OnMenuRemoveSelectedComponents()
	{
		// var index = (int)userData;
		// entityConfigAsset.components.RemoveAt(index);
		// for (int i = 0; i < _selectedComponents.Length; i++)
		// {
		// 	if (_selectedComponents[i])
		// 		entityConfigAsset.components.RemoveAt(i);
		// }
		for (int i=0; i<entityConfigAsset.components.Count; i++)
		{
			if (_selectedComponents[i])
			{
				_selectedComponents[i] = false;
				entityConfigAsset.components.RemoveAt(i);
				i--;
			}
		}
		RegenerateEditors();
	}
	
	private void OnMenuSelectComponentAsset(object userData)
	{
		var index = (int)userData;
		Selection.activeObject = AssetDatabase.LoadAssetAtPath<ScriptableObject>(AssetDatabase.GetAssetPath(entityConfigAsset.components[index]));
	}

	private void ApplyChanges()
	{
		EditorUtility.SetDirty(entityConfigAsset);
		serializedObject.ApplyModifiedProperties();
		_componentsProperty.serializedObject.Update();
	}

	private void RegenerateEditors()
	{
		editorInstance = (EntityConfigEditorInstance)ScriptableObject.CreateInstance(typeof(EntityConfigEditorInstance));

		int editorCount = entityConfigAsset.components.Count;
		editorInstance.editors = new Editor[editorCount];
		_headerRects = new Rect[editorCount];
		_selectedComponents = new bool[editorCount];

		for (int i = 0; i < editorCount; i++)
		{
			if (entityConfigAsset.components[i] == null)
				continue;

			editorInstance.editors[i] = Editor.CreateEditor(entityConfigAsset.components[i]);
		}
	}

	public void MoveListItem<T>(ref List<T> list, int oldIndex, int newIndex)
	{
		T item = list[oldIndex];
		list.RemoveAt(oldIndex);
		list.Insert(newIndex, item);
	}
}