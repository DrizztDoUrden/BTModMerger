## What are moddiffs?

Moddiffs are XML files that are supposed to contain a modded file definition that contains only exact differences from some base file, usually from vanilla game. This is achieved by having special nodes and namespaces in the XML file.

## How do special nodes show what they affect?

First thing we should know is our relative position. Every node in a moddiff file has some special location in base file it is affecting. For example for `Human.xml` it can be `Character[@human:Human]/health`. It is read right to left and means a `health` inside node `Character`, identified as `@human:Human` among other Characters in its scope. Another expample is `ItemSet[1]` means **second** `ItemSet` in the current scope. Scope means content of some element, ie its child direct elements. Any regular (without xml namespace, the xyz: thingy) element creates such scope. Some special nodes in diff can alter they relative location and scope. They are described bellow

## Some difference to vanilla game XML files

First, the tool uses xml namespaces to differentiate its own syntax from vanilla elements. I would tell a bit about them in the next chapter
Second, **all** moddiff syntax elements are **case sensitive**. `bTmm:Diff` and `btmm:diff` are both incorrect and would either produce incorrect behaviour. Most likely tool would interpret them as vanilla elements, I did not add a lot of checks for that kind of errors.

## btmm:Diff and used XML namespaces

This element simply defines the root of the diff. It can be empty, but such files are not generally useful. For convenience of reading the diff I suggest adding all used namespaces to this element. This is done via `xmlns:xyz` attribute. The following namespaces are used by the tool:
* `xmlns:btmm="https://github.com/DrizztDoUrden/BTModMerger"` - this is a general one, present on special elements
* `xmlns:add="https://github.com/DrizztDoUrden/BTModMerger/FakeURI/Add"` - this one defines adding operation. For attributes it can also mean replacing the previous value
* `xmlns:remove="https://github.com/DrizztDoUrden/BTModMerger/FakeURI/Remove"` - this one dfines removal operation.

`xyz:` at the start of node (element or attribute) name means and XML namespace the element belongs to. `xmlns` namespace is used to declare other namespaces, thus we do it just once in the `btmm:Diff` element, that affects every single child of that.

An example:

```xml
<btmm:Diff
	xmlns:btmm="https://github.com/DrizztDoUrden/BTModMerger"
	xmlns:add="https://github.com/DrizztDoUrden/BTModMerger/FakeURI/Add"
	xmlns:remove="https://github.com/DrizztDoUrden/BTModMerger/FakeURI/Remove">
	<!--A not very useful empty diff-->
</btmm:Diff>
```

## btmm:Into

This element main purpose is to change current relative location and scope. It requires a `btmm:Path` attribute, which is the working horse. Value of this attribute alters the relative location of `btmm::Into` element and all of its children. Optionally, it can have other attributes with any names and namspace either `add:` or `remove:`. They would respectively set or remove the specified atributes.

An example:

```xml
<btmm:Diff xmlns:btmm="https://github.com/DrizztDoUrden/BTModMerger" xmlns:add="https://github.com/DrizztDoUrden/BTModMerger/FakeURI/Add" xmlns:remove="https://github.com/DrizztDoUrden/BTModMerger/FakeURI/Remove">
	<!--  ^ You have to add actual namespaces here, like in example above-->
	<!-- We are now in a root scope, meaning attached to no specific element in the base XML. -->
	<btmm:Into btmm:Path="Character[@human:Human]">
		<!-- We are now in scope of an element <Character group="human" species="Human ... /> -->
		<btmm:Into btmm:Path="health" add:Vitality="150" remove:DoesBleed="">
			<!-- We are now in scope of an element <heatlh /> inside previous scope -->
			<!-- The <health /> node is also designated to have its Vitality attribute set to value 150 and DoesBleed attribute to be removed. -->
		</btmm:Into>
	</btmm:Into>
</btmm:Diff>
```

## btmm:UpdateAttributes

This element works like into except for it does not allow any child elements. It is introduces for readability purposes. It supports the same attribute set.

An example:

```xml
<btmm:Diff xmlns:btmm="https://github.com/DrizztDoUrden/BTModMerger" xmlns:add="https://github.com/DrizztDoUrden/BTModMerger/FakeURI/Add" xmlns:remove="https://github.com/DrizztDoUrden/BTModMerger/FakeURI/Remove">
	<btmm:Into btmm:Path="Character[@human:Human]">
		<btmm:UpdateAttributes btmm:Path="health" add:Vitality="150" remove:DoesBleed="" />
			<!-- The <health /> node inside Character[@human:Human] is designated to have its Vitality attribute set to value 150 and DoesBleed attribute to be removed. -->
	</btmm:Into>
</btmm:Diff>
```

## btmm:RemoveElement

This element designates its target to be removed from the tree. It supports only `btmm:Path` attribute, that specifies the removed element in relation to the current scope.

An example:

```xml
<btmm:Diff xmlns:btmm="https://github.com/DrizztDoUrden/BTModMerger" xmlns:add="https://github.com/DrizztDoUrden/BTModMerger/FakeURI/Add" xmlns:remove="https://github.com/DrizztDoUrden/BTModMerger/FakeURI/Remove">
	<btmm:Into btmm:Path="Character[@human:Human]">
		<btmm:RemoveElement btmm:Path="health" />
			<!-- The <health /> node inside Character[@human:Human] is designated to be removed with all its children. -->
	</btmm:Into>
</btmm:Diff>
```

## add:xyz and no namespace elements

Any element that has either no namespace or a namespace `add:` means adding the element to target location. It supports `btmm:Path` attribute, like usual, but also `btmm:Amount`, which means several copies added in sequence.

```xml
<btmm:Diff xmlns:btmm="https://github.com/DrizztDoUrden/BTModMerger" xmlns:add="https://github.com/DrizztDoUrden/BTModMerger/FakeURI/Add" xmlns:remove="https://github.com/DrizztDoUrden/BTModMerger/FakeURI/Remove">
	<add:Wearable btmm:Path="Character[@human:Human]/HeadAttachments" replacewhenwearinghat="1" tags="male" type="Hair">
		<sprite name="Hair 18" sheetindex="3,1" texture="Content/Characters/Human/Human_male_hair.png" />
	</add:Wearable>
	<!-- We have just added a specific Wearable element to current scope -->
	<btmm:Into btmm:Path="Character[@human:Human]/HeadAttachments" btmm:Amount=2>
		<Wearable replacewhenwearinghat="1" tags="male" type="Hair">
			<sprite name="Hair 17" sheetindex="3,1" texture="Content/Characters/Human/Human_male_hair.png" />
		</Wearable>
	</btmm:Into>
	<!-- We have just added two copies of a specific Wearable element to sane scope as previous one -->
</btmm:Diff>
```

## remove:xyz elements

When an element has a namespace `remove:` it means a verbatim removal. It supports attribute `btmm:Path`, but it specifies parent of the removed element instead. It is convenient for removing something from a container with relatively simple items that are difficult to address otherwise. For example, items in the jobs starting inventory. It also supports `btmm:Amount` atribute, meaning removal of several copies of the same item.

```xml
<btmm:Diff xmlns:btmm="https://github.com/DrizztDoUrden/BTModMerger" xmlns:add="https://github.com/DrizztDoUrden/BTModMerger/FakeURI/Add" xmlns:remove="https://github.com/DrizztDoUrden/BTModMerger/FakeURI/Remove">
	<remove:Wearable btmm:Path="Character[@human:Human]/HeadAttachments" type="Beard" tags="male">
		<sprite name="Beard 9" texture="Content/Characters/Human/Human_beards.png" sheetindex="0,2" />
	</remove:Wearable>
	<!-- We have just removed a specific Wearable from our character HeadAttachment options -->
</btmm:Diff>
```
