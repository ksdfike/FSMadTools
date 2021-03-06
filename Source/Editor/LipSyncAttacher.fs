﻿namespace FSMadTools

open FSMadTools.Utility
open UnityEngine
open UnityEditor
open VRCSDK2
open UniRx

type LipSyncAttacher() =
  inherit ToolBase()
  let gameObject = new ReactiveProperty<GameObject option>()
  let descriptor = new ReactiveProperty<VRC_AvatarDescriptor option>()
  let faceMesh = new ReactiveProperty<SkinnedMeshRenderer option>()
  let mutable lipSyncVisemes = Array.zeroCreate 0
  let mutable notExistVisemes = Array.zeroCreate 0

  let visemes =
    [| "sil"; "PP"; "FF"; "TH"; "DD"; "kk"; "CH"; "SS"; "nn"; "RR"; "aa"; "E"; "ih"; "oh"; "ou" |]

  let vrcViseme (name : string) : string =
    sprintf "vrc.v_%s" (String.lowercase name)

  let vrcVisemes = visemes |> Array.map vrcViseme

  do
    faceMesh.Subscribe(Option.iter (fun (s : SkinnedMeshRenderer) ->
      descriptor.Value |> Option.iter (fun desc -> desc.VisemeSkinnedMesh <- s)
      lipSyncVisemes <-
        s.sharedMesh
        |> Unity.Mesh.blendShapeNames
        |> List.filter (fun x -> vrcVisemes |> Array.exists (fun y -> x.ToLower() = y))
        |> List.toArray
      notExistVisemes <-
        visemes |> Array.filter (fun x ->
          lipSyncVisemes |> Array.exists (fun y -> vrcViseme x = String.lowercase y) |> not
        )
      )
    ) |> ignore

  let attachLipSync (lipSyncVisemes : string[]) (desc : VRC_AvatarDescriptor) =
    desc.lipSync <- VRC_AvatarDescriptor.LipSyncStyle.VisemeBlendShape

    // lipSyncVisemesをvisemesと同じ順にする
    let lipSyncVisemes = vrcVisemes |> Array.map (fun name ->
      lipSyncVisemes |> Array.find (fun x -> String.lowercase x = name)
    )

    desc.VisemeBlendShapes <- lipSyncVisemes

  let drawAttacher (mesh : Mesh) (desc : VRC_AvatarDescriptor) =
    match notExistVisemes with
    | [||] ->
      if GUILayout.Button("Attach Lip Sync") then
        attachLipSync lipSyncVisemes desc
    | xs ->
      xs |> Array.iter (fun x ->
        EditorError.error (sprintf "%sが存在しません" x)
      )

  let drawGUI (desc : VRC_AvatarDescriptor) =
    faceMesh.Value <- CustomGUILayout.objectField "Face Mesh" desc.VisemeSkinnedMesh true

    match desc.VisemeSkinnedMesh |> Option.ofObj with
    | None -> EditorError.error "Face Meshを指定してください"
    | Some s -> drawAttacher s.sharedMesh desc

  override __.draw () =
    gameObject.Value <- Unity.Selection.activeGameObject ()

    match gameObject.Value with
    | None -> EditorError.pleaseSelectGameObject ()
    | Some go ->
      descriptor.Value <- go |> Unity.GameObject.getComponent
      match descriptor.Value with
      | None -> EditorError.avatarDescriptorDoesNotExist ()
      | Some desc ->
        drawGUI desc