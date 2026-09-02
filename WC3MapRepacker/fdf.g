program :
    (statement)*
    ;

statement:
    STRING_LIST OPEN_CURLY (ID STRING_LITERAL COMMA)*? CLOSE_CURLY # StringListStatement
    |
    INCLUDE_FILE STRING_LITERAL COMMA # IncludeStatement
    |
    frame # FrameStatement
    ;

frame:
    FRAME STRING_LITERAL STRING_LITERAL OPEN_CURLY frame_element* CLOSE_CURLY # SimpleFrameDefinition
    |
    FRAME STRING_LITERAL STRING_LITERAL INHERITS STRING_LITERAL OPEN_CURLY frame_element* CLOSE_CURLY # FrameDefinition
    |
    FRAME STRING_LITERAL STRING_LITERAL INHERITS WITHCHILDREN STRING_LITERAL OPEN_CURLY frame_element* CLOSE_CURLY # FrameDefinitionWithChildren
    ;