# fastgithub 命令行自动补全（PowerShell）
# 安装：Add-Content $PROFILE ". /path/to/FastGithub-Project/completions/fastgithub-completion.ps1"
# 卸载：重启 PowerShell 后自动失效，或删除 $PROFILE 中对应行

Register-ArgumentCompleter -CommandName fastgithub, fastgithub.exe -ScriptBlock {
    param($wordToComplete, $commandAst, $cursorPosition)

    $subcommands = @('start', 'stop', 'config')
    $options = @('-h', '--help', '--port', '--listen', '--dns-listen', '--dns-port', '--dns', '--log-level', '--udp-port')
    $configActions = @('list', 'get', 'set', 'unset')

    $elements = @($commandAst.CommandElements)
    if ($elements.Count -eq 0) { return }

    # 定位光标所在的元素：光标落在某个元素的起止偏移之间，说明正在补全该元素；
    # 光标不在任何元素内（越过全部元素末尾），说明是尾随空白，正在补全新参数。
    # 注意不能用 CommandAst.ToString() 判断尾随空格——它会丢弃尾随空白。
    $currentIndex = -1
    for ($i = 0; $i -lt $elements.Count; $i++) {
        if ($cursorPosition -ge $elements[$i].Extent.StartOffset -and $cursorPosition -le $elements[$i].Extent.EndOffset) {
            $currentIndex = $i
            break
        }
    }

    if ($currentIndex -lt 0) {
        # 补全新参数：已完整参数个数 = 元素数 - 1（去掉命令名）
        $argIndex = $elements.Count - 1
        $prev = if ($elements.Count -ge 2) { $elements[$elements.Count - 1].ToString() } else { '' }
    }
    else {
        # 补全当前元素：当前参数下标 = 元素下标 - 1（去掉命令名）
        $argIndex = $currentIndex - 1
        $prev = if ($currentIndex -ge 2) { $elements[$currentIndex - 1].ToString() } else { '' }
    }

    function Add-Completion($items) {
        foreach ($item in $items) {
            if ($item -and $item.ToString().StartsWith($wordToComplete, [StringComparison]::OrdinalIgnoreCase)) {
                [System.Management.Automation.CompletionResult]::new($item, $item, 'ParameterValue', $item)
            }
        }
    }

    # 第一层：指令与参数
    if ($argIndex -le 0) {
        Add-Completion ($subcommands + $options)
        return
    }

    # config 子命令
    if ($elements[1].ToString() -eq 'config') {
        # 第二层：动作
        if ($argIndex -eq 1) {
            Add-Completion $configActions
            return
        }

        # 第三层：仅键位（第3个参数）补全配置键，运行动态读取；
        # config get/unset 只收一个键，config set 的第4位是值，均不再补全键
        if ($argIndex -eq 2 -and $elements[2].ToString() -in 'get', 'set', 'unset') {
            try {
                $keys = & fastgithub config list 2>$null | ForEach-Object { ($_ -split '=', 2)[0] }
                Add-Completion $keys
            }
            catch {
            }
            return
        }
    }

    # 参数的值：给出常用取值
    switch ($prev) {
        '--port' { Add-Completion @('38457', '38458', '8080', '9000'); return }
        '--udp-port' { Add-Completion @('38457', '38458', '8080', '9000'); return }
        '--dns-port' { Add-Completion @('53', '5353', '8053'); return }
        '--listen' { Add-Completion @('localhost', '0.0.0.0'); return }
        '--dns-listen' { Add-Completion @('localhost', '0.0.0.0'); return }
        '--log-level' { Add-Completion @('Verbose', 'Debug', 'Information', 'Warning', 'Error', 'Fatal'); return }
        '--dns' { Add-Completion @('8.8.8.8:53', '119.29.29.29:53', '114.114.114.114:53'); return }
    }
}
