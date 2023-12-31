﻿using System;
using System.Collections.Generic;
using Dalamud.Logging;

using FFXIVClientStructs.FFXIV.Client.System.Memory;
using FFXIVClientStructs.FFXIV.Component.GUI;

namespace ReadyCheckHelper;

internal static unsafe class AtkNodeHelpers
{
	/*internal static AtkImageNode* GetImageNodeByIDFromComponent( AtkComponentBase* pComponentBase, uint nodeID )
	{
		if( pComponentBase != null )
		{
			for( int i = 0; i < pComponentBase->UldManager.NodeListCount; ++i )
			{
				var pNodeToCheck = pComponentBase->UldManager.NodeList[i];
				if( pNodeToCheck != null && pNodeToCheck->NodeID == nodeID ) return (AtkImageNode*)pNodeToCheck;
			}
		}

		return null;
	}*/

	internal static AtkImageNode* GetImageNodeByID( AtkUnitBase* pAddon, uint nodeID )
	{
		if( pAddon == null ) return null;
		for( var i = 0; i < pAddon->UldManager.NodeListCount; ++i )
		{
			if( pAddon->UldManager.NodeList[i] == null ) continue;
			if( pAddon->UldManager.NodeList[i]->NodeID == nodeID )
			{
				return (AtkImageNode*)pAddon->UldManager.NodeList[i];
			}
		}
		return null;
	}

	internal static void AttachImageNode( AtkUnitBase* pAddon, AtkImageNode* pNode )
	{
		if( pAddon == null ) return;

		if( pNode != null )
		{
			var lastNode = pAddon->RootNode;
			if( lastNode->ChildNode != null )
			{
				lastNode = lastNode->ChildNode;
				while( lastNode->PrevSiblingNode != null )
				{
					lastNode = lastNode->PrevSiblingNode;
				}

				pNode->AtkResNode.NextSiblingNode = lastNode;
				pNode->AtkResNode.ParentNode = pAddon->RootNode;
				lastNode->PrevSiblingNode = (AtkResNode*)pNode;
			}
			else
			{
				lastNode->ChildNode = (AtkResNode*)pNode;
				pNode->AtkResNode.ParentNode = lastNode;
			}

			pAddon->UldManager.UpdateDrawNodeList();
		}
	}

	/*internal static void AppendImageNodeToComponent( AtkComponentBase* pComponentBase, AtkImageNode* pNode, AtkComponentNode* pParentNode )
	{
		if( pComponentBase == null ||
			pNode == null ||
			pParentNode == null ) return;

		if( pComponentBase->UldManager.NodeListCount > 0 )
		{
			var lastNode = pComponentBase->UldManager.NodeList[0];
			while( lastNode->PrevSiblingNode != null )
			{
				lastNode = lastNode->PrevSiblingNode;
			}

			pNode->AtkResNode.NextSiblingNode = lastNode;
			pNode->AtkResNode.PrevSiblingNode = null;
			pNode->AtkResNode.ParentNode = (AtkResNode*)pParentNode;
			lastNode->PrevSiblingNode = (AtkResNode*)pNode;

			pComponentBase->UldManager.UpdateDrawNodeList();
		}
	}*/

	internal static AtkImageNode* CreateOrphanImageNode( uint nodeID, List<AtkUldPart> partInfo, GameAddonEnum addon )
	{
		if( partInfo == null || partInfo.Count < 1 )
		{
			PluginLog.LogError( "Invalid partInfo list.  A non-null list with one or more parts must be provided." );
			return null;
		}

		var pNewNode = (AtkImageNode*)IMemorySpace.GetUISpace()->Malloc( (ulong)sizeof(AtkImageNode), 8 );
		if( pNewNode != null )
		{
			IMemorySpace.Memset( pNewNode, 0, (ulong)sizeof( AtkImageNode ) );
			pNewNode->Ctor();

			pNewNode->AtkResNode.Type = NodeType.Image;
			pNewNode->AtkResNode.NodeID = nodeID;
			pNewNode->AtkResNode.Flags = (short)( NodeFlags.AnchorLeft | NodeFlags.AnchorTop );
			pNewNode->AtkResNode.DrawFlags = 0;
			pNewNode->WrapMode = 1;
			pNewNode->Flags = 0;

			pNewNode->AtkResNode.SetPositionShort( 0, 0 );
			pNewNode->AtkResNode.SetWidth( DefaultImageNodeWidth );
			pNewNode->AtkResNode.SetHeight( DefaultImageNodeHeight );

			//***** TODO:	The game uses the same parts list, parts, and assets for all nodes that do the same thing (i.e., all checkmark nodes in the item
			//				search window are the same, all checkmarks in the card selling window are the same, etc.).  The main thing stopping us from doing
			//				that is that I don't know whether these parts list get destroyed if the UI cleans up a node, and if so, how to know to make new
			//				ones, so right now it seems safer to make parts lists, etc. for each node.  The game seems to do some synchronization/consolidation
			//				of parts lists with the same ID anyway, so hopefully it's not a problem.

			UInt32 partsListID = addon switch
			{
				GameAddonEnum.PartyList => PartyPartListID,
				GameAddonEnum.AllianceList1 => Alliance1PartListID,
				GameAddonEnum.AllianceList2 => Alliance2PartListID,
				GameAddonEnum.CrossWorldAllianceList => CrossWorldAlliancePartListID,
				_ => throw( new Exception( "Invalid addon specified while determining parts list ID." ) ),
			};

			var pPartsList = (AtkUldPartsList*)IMemorySpace.GetUISpace()->Malloc( (ulong)sizeof(AtkUldPartsList), 8 );
			if( pPartsList == null )
			{
				PluginLog.LogError( "Failed to allocate memory for parts list." );
				pNewNode->AtkResNode.Destroy( true );
				return null;
			}
			pPartsList->Id = partsListID;
			pPartsList->PartCount = (uint)partInfo.Count;

			var pParts = (AtkUldPart*)IMemorySpace.GetUISpace()->Malloc( (ulong)sizeof(AtkUldPart) * (ulong)partInfo.Count, 8 );
			if( pParts == null )
			{
				PluginLog.LogError( "Failed to allocate memory for parts." );
				IMemorySpace.Free( pPartsList, (ulong)sizeof( AtkUldPartsList ) );
				pNewNode->AtkResNode.Destroy( true );
				return null;
			}

			for( int i = 0; i < partInfo.Count; ++i )
			{
				pParts[i].U = partInfo[i].U;
				pParts[i].V = partInfo[i].V;
				pParts[i].Width = partInfo[i].Width;
				pParts[i].Height = partInfo[i].Height;
			}

			pPartsList->Parts = pParts;

			var pAsset = (AtkUldAsset*)IMemorySpace.GetUISpace()->Malloc( (ulong)sizeof(AtkUldAsset), 8 );
			if( pAsset == null )
			{
				PluginLog.LogError( "Failed to allocate memory for asset." );
				IMemorySpace.Free( pParts, (ulong)sizeof( AtkUldPart ) * (ulong)partInfo.Count );
				IMemorySpace.Free( pPartsList, (ulong)sizeof( AtkUldPartsList ) );
				pNewNode->AtkResNode.Destroy( true );
				return null;
			}
			for( int i = 0; i < partInfo.Count; ++i )
			{
				pAsset->Id = 0xCA45C0;	//	I have no idea for what this ID is used.  Keep it something out of the way I guess.
				pAsset->AtkTexture.Ctor();
				pParts[i].UldAsset = pAsset;
			}

			pNewNode->PartsList = pPartsList;

			//***** TODO: Is it possible to have multiple textures loaded at the same time (different ones associated with different parts)?  This would be nice to add in a question mark and CD symbol as display options without reloading textures all the time.
			pNewNode->PartId = 0;
			pNewNode->LoadTexture( "ui/uld/ReadyCheck_hr1.tex" );

			pNewNode->AtkResNode.Color.A = 0xFF;
			pNewNode->AtkResNode.Color.R = 0xFF;
			pNewNode->AtkResNode.Color.G = 0xFF;
			pNewNode->AtkResNode.Color.B = 0xFF;
		}

		return pNewNode;
	}

	internal static void HideNode( AtkUnitBase* pAddon, uint nodeID )
	{
		var pNode = GetImageNodeByID( pAddon, nodeID );
		if( pNode != null ) ( (AtkResNode*)pNode )->ToggleVisibility( false );
	}

	internal const ushort DefaultImageNodeWidth = 48;
	internal const ushort DefaultImageNodeHeight = 48;

	internal const UInt32 PartyPartListID				= 0xB934C0;	//YOLO hoping for no collisions.
	internal const UInt32 Alliance1PartListID			= 0xB934C1;
	internal const UInt32 Alliance2PartListID			= 0xB934C2;
	internal const UInt32 CrossWorldAlliancePartListID	= 0xB934C3;
}
