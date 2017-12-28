## Prefab Editor

Prefab Editor - is Unity3d tool for editing prefabs in runtime(playmode). You can change anything in prefab instance (root object or any child) in runtime, then select changes you want to apply and press apply. It is just that simple. 

For example, it is very convenient while editing a UI. You tweak positions, colors, and other properties of your elements and than you have to memorize changes (or write them down), press stop, find prefab and apply them again. With Prefab Editor you can just apply selectively changes you need. 

Key Features 
- Automatically searches for prefab you selected. If more than one is found - you can select the one you need. 
- Supports adding new components, deleting existing compoents, and changing any component property. 
- Supports changing reference properties. 
- Optimized to work very fast even on huge projects. 

Also available on the Unity Store: https://assetstore.unity.com/packages/tools/utilities/prefab-editor-24895

## Usage

* Copy the `RuntimePrefabEditor` folder somewhere under the `Assets` folder. 
* Open Unity3d.
* Open `Window/Prefab Editor`.
* Select a GameObject in your scene to edit. Prefab Editor will show the prefab associated with that GameObject, it could be that there are few.
* Edit your GameObject.
* Prefab Editor show changes, select changes you want to apply and press `Apply Changes To Prefab` button.

## Usage in pictures

![Alt text](/Images/step0.jpg?raw=true "Step 0")
![Alt text](/Images/step1.jpg?raw=true "Step 1")
![Alt text](/Images/step2.jpg?raw=true "Step 2")
![Alt text](/Images/step3.jpg?raw=true "Step 3")

## Misc

It was written few years ago and no longer developed. Though it should work quite well still and I hope someone will find it as useful as I once did.